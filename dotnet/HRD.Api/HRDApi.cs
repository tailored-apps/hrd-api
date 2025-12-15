using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using HRD.Api.Exceptions;

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("HRD.Api.Tests")]

namespace HRD.Api
{
    public partial class HRDApi : IDisposable
    {
        private const string DefaultHost = "api.hrd.pl";
        private const int DefaultPort = 9999;
        private const int DefaultTimeout = 3000; // 3 seconds

        private readonly string _host;
        private readonly int _port;
        private readonly int _timeout;
        private TcpClient? _client;
        private SslStream? _sslStream;
        private byte[] _apiHash; // Binary hash derived from hex config
        private string? _token;
        private bool _debug = false;
        protected readonly XNamespace _ns = "http://api.hrd.pl/api/";

        public HRDApi(byte[] apiHash, string host = DefaultHost, int port = DefaultPort, int timeout = DefaultTimeout)
        {
            _apiHash = apiHash;
            _host = host;
            _port = port;
            _timeout = timeout;
        }

        public static HRDApi Create(string apiHashHex, string login, string password)
        {
            // In PHP: hex2bin($config['apiHash'])
            byte[] hashBytes = Convert.FromHexString(apiHashHex);
            var api = new HRDApi(hashBytes);
            api.Login(login, password);
            return api;
        }

        private void Connect()
        {
            if (_client != null && _client.Connected) return;

            try
            {
                _client = new TcpClient();
                // Connect with timeout
                var result = _client.BeginConnect(_host, _port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(_timeout);
                if (!success)
                {
                    throw new HRDApiCommunicationException("Connection timed out");
                }
                _client.EndConnect(result);

                _sslStream = new SslStream(_client.GetStream(), false,
                    new RemoteCertificateValidationCallback((sender, cert, chain, errors) => true)); // Accepting all certs as per PHP's allow_self_signed logic, should precise for prod

                _sslStream.AuthenticateAsClient(_host);
            }
            catch (Exception ex)
            {
                throw new HRDApiCommunicationException("Connect error", ex.Message);
            }
        }

        private void Send(XDocument xml)
        {
            Connect();

            string xmlString;
            using (var sw = new StringWriter()) // Preserve encoding? PHP saveXML usually includes declaration
            {
                // To match PHP saveXML behavior which usually outputs UTF-8
                // We'll just use ToString() with DisableFormatting to avoid extra whitespace issues? 
                // PHP's saveXML adds <?xml version="1.0"?> usually
                xmlString = xml.Declaration + xml.ToString(SaveOptions.DisableFormatting);
                // Actually XDocument.ToString() usually omits declaration. 
                // Let's force it if missing or just rely on what we build.
                if (xml.Declaration == null)
                {
                    xmlString = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + xmlString;
                }
            }


            byte[] finalPacket = BuildPacket(_apiHash, xmlString);

            try
            {
                _sslStream.Write(finalPacket);
                _sslStream.Flush();
            }
            catch (Exception ex)
            {
                throw new HRDApiCommunicationException("Send error", ex.Message);
            }
        }

        internal static byte[] BuildPacket(byte[] apiHash, string xmlString)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(xmlString);

            // Concatenate data + hash
            byte[] toHash = new byte[dataBytes.Length + apiHash.Length];
            Buffer.BlockCopy(dataBytes, 0, toHash, 0, dataBytes.Length);
            Buffer.BlockCopy(apiHash, 0, toHash, dataBytes.Length, apiHash.Length);

            byte[] hash;
            using (var sha512 = SHA512.Create())
            {
                hash = sha512.ComputeHash(toHash);
            }

            // $data = $hash . $data;
            byte[] packet = new byte[hash.Length + dataBytes.Length];
            Buffer.BlockCopy(hash, 0, packet, 0, hash.Length);
            Buffer.BlockCopy(dataBytes, 0, packet, hash.Length, dataBytes.Length);

            // $data = pack('N', strlen($data)) . $data;
            int length = packet.Length;
            byte[] lengthBytes = BitConverter.GetBytes(length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }

            byte[] finalPacket = new byte[4 + packet.Length];
            Buffer.BlockCopy(lengthBytes, 0, finalPacket, 0, 4);
            Buffer.BlockCopy(packet, 0, finalPacket, 4, packet.Length);

            return finalPacket;
        }

        private XDocument Read()
        {
            byte[] lenBuffer = new byte[4];
            int read = ReadFull(_sslStream, lenBuffer, 4);
            if (read != 4) throw new HRDApiCommunicationException("Read error: failed to read length");

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lenBuffer);
            }
            int length = BitConverter.ToInt32(lenBuffer, 0);

            byte[] dataBuffer = new byte[length];
            read = ReadFull(_sslStream, dataBuffer, length);
            if (read != length) throw new HRDApiCommunicationException("Read error: incomplete data");

            string responseString = Encoding.UTF8.GetString(dataBuffer);

            // Remove xmlns to make XLinq easier? PHP does:
            // str_replace('xmlns="http://api.hrd.pl/api/"', '', $data)
            responseString = responseString.Replace("xmlns=\"http://api.hrd.pl/api/\"", "");

            try
            {
                return XDocument.Parse(responseString);
            }
            catch (Exception ex)
            {
                throw new HRDApiIncorrectDataReceivedException("Invalid XML response: " + ex.Message);
            }
        }

        private int ReadFull(Stream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, totalRead, count - totalRead);
                if (read == 0) return totalRead;
                totalRead += read;
            }
            return totalRead;
        }

        public void Dispose()
        {
            _sslStream?.Dispose();
            _client?.Dispose();
        }

        // Helper to Create Basic XML
        private (XDocument, XElement) GetApiDocument(string name1, string name2 = null)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null));
            var api = new XElement(_ns + "api");
            doc.Add(api);

            var elem1 = new XElement(_ns + name1);
            api.Add(elem1);

            if (name2 != null)
            {
                var elem2 = new XElement(_ns + name2);
                elem1.Add(elem2);
                return (doc, elem2);
            }

            return (doc, elem1);
        }

        // --- Core Methods ---

        public void Login(string login, string password, string type = "partnerApi")
        {
            // Implement loginByPass logic
            var (doc, elem) = GetApiDocument("login");
            elem.Add(new XElement(_ns + "login", login));
            elem.Add(new XElement(_ns + "pass", password));
            elem.Add(new XElement(_ns + "type", type));

            Send(doc);
            var response = Read();

            // xpath Query: /api/token/text()
            // Since we stripped xmlns, we can just query
            var token = response.Element("api")?.Element("token")?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                _token = token;
            }
            else
            {
                CheckError(response);
            }
        }

        private void CheckError(XDocument response)
        {
            var msg = response.Element("api")?.Element("message")?.Value;
            if (!string.IsNullOrEmpty(msg))
            {
                throw new HRDApiCommunicationException("api_error", msg);
            }
            // If we are here and passed CheckError, maybe it was successful but logic handled elsewhere?
            // Or maybe unknown error.
        }

        // --- Partner Methods ---

        public (decimal balance, decimal restrictedBalance) PartnerGetBalance()
        {
            var (doc, elem) = GetApiDocument("partner", "getBalance");
            Send(doc);
            var response = Read();

            var result = response.Element("api")?.Element("partner")?.Element("getBalance");
            if (result == null)
            {
                CheckError(response);
                throw new HRDApiCommunicationException("Unexpected response format");
            }

            // PHP: return $this->sendArrayResponse($dom, 'partner/getBalance/*');
            // sendArrayResponse parses all children.
            // Expected: balance, restricted_balance

            decimal balance = 0;
            decimal restricted = 0;

            var b = result.Element("balance")?.Value;
            var rb = result.Element("restricted_balance")?.Value;

            if (decimal.TryParse(b, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v1)) balance = v1;
            if (decimal.TryParse(rb, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v2)) restricted = v2;

            return (balance, restricted);
        }
    }
}
