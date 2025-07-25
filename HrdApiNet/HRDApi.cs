using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace HrdApiNet;

public class HRDApi : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _timeout;
    private byte[] _hash;
    private SslStream? _stream;

    public string? Token { get; private set; }

    public HRDApi(string host = "api.hrd.pl", int port = 9999, int timeoutSeconds = 3)
    {
        _host = host;
        _port = port;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _hash = Array.Empty<byte>();
    }

    public void SetHash(byte[] hash) => _hash = hash;

    public async Task LoginAsync(string login, string pass, string type)
    {
        var (dom, elem) = CreateApiDocument("login");
        elem.AppendChild(dom.CreateElement("login")).InnerText = login;
        elem.AppendChild(dom.CreateElement("pass")).InnerText = pass;
        elem.AppendChild(dom.CreateElement("type")).InnerText = type;

        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);

        var tokenNode = response.SelectSingleNode("/api/token");
        if (tokenNode != null)
        {
            Token = tokenNode.InnerText;
        }
    }

    public async Task<Dictionary<string, string>> PartnerGetBalanceAsync()
    {
        var (dom, elem) = CreateApiDocument("partner", "getBalance");
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);

        var result = new Dictionary<string, string>();
        foreach (XmlNode node in response.SelectNodes("/api/partner/getBalance/*")!)
        {
            result[node.Name] = node.InnerText;
        }
        return result;
    }

    public async Task<Dictionary<string, string>?> PollGetAsync()
    {
        var (dom, elem) = CreateApiDocument("poll", "get");
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var nodes = response.SelectNodes("/api/poll/get/*");
        if (nodes == null || nodes.Count == 0)
            return null;
        var result = new Dictionary<string, string>();
        foreach (XmlNode node in nodes)
        {
            result[node.Name] = node.InnerText;
        }
        return result;
    }

    public async Task PollAckAsync(int id)
    {
        var (dom, elem) = CreateApiDocument("poll", "ack");
        elem.AppendChild(dom.CreateElement("id")).InnerText = id.ToString();
        await SendAsync(dom).ConfigureAwait(false);
        await ReadAsync().ConfigureAwait(false);
    }

    public async Task<Dictionary<string, string>?> CertInfoAsync(int id)
    {
        var (dom, elem) = CreateApiDocument("cert", "info");
        elem.AppendChild(dom.CreateElement("id")).InnerText = id.ToString();
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var nodes = response.SelectNodes("/api/cert/info/*");
        if (nodes == null || nodes.Count == 0)
            return null;
        var result = new Dictionary<string, string>();
        foreach (XmlNode node in nodes)
        {
            result[node.Name] = node.InnerText;
        }
        return result;
    }

    public async Task<Dictionary<string, string>?> CertCreateAsync(Dictionary<string, string> data)
    {
        var (dom, elem) = CreateApiDocument("cert", "create");
        foreach (var kv in data)
        {
            elem.AppendChild(dom.CreateElement(kv.Key)).InnerText = kv.Value;
        }
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var nodes = response.SelectNodes("/api/cert/create/*");
        if (nodes == null || nodes.Count == 0)
            return null;
        var result = new Dictionary<string, string>();
        foreach (XmlNode node in nodes)
        {
            result[node.Name] = node.InnerText;
        }
        return result;
    }

    public async Task<int> CertRenewAsync(string id, int period, string periodUnit = "y")
    {
        var (dom, elem) = CreateApiDocument("cert", "renew");
        elem.AppendChild(dom.CreateElement("id")).InnerText = id;
        var periodElem = dom.CreateElement("period");
        periodElem.InnerText = period.ToString();
        periodElem.SetAttribute("unit", periodUnit);
        elem.AppendChild(periodElem);
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var node = response.SelectSingleNode("/api/cert/renew/actionId");
        return node != null ? int.Parse(node.InnerText) : 0;
    }

    public async Task<Dictionary<string, string>?> DomainInfoAsync(string name)
    {
        var (dom, elem) = CreateApiDocument("domain", "info");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var nodes = response.SelectNodes("/api/domain/info/*");
        if (nodes == null || nodes.Count == 0)
            return null;
        var result = new Dictionary<string, string>();
        foreach (XmlNode node in nodes)
        {
            result[node.Name] = node.InnerText;
        }
        return result;
    }

    public async Task<int> DomainCreateAsync(string name, int user, IEnumerable<string> ns)
    {
        var (dom, elem) = CreateApiDocument("domain", "create");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        elem.AppendChild(dom.CreateElement("user")).InnerText = user.ToString();
        var nsElem = dom.CreateElement("ns");
        foreach (var n in ns)
        {
            nsElem.AppendChild(dom.CreateElement("ns")).AppendChild(dom.CreateElement("name")).InnerText = n;
        }
        elem.AppendChild(nsElem);
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var idNode = response.SelectSingleNode("/api/domain/create/actionId");
        return idNode != null ? int.Parse(idNode.InnerText) : 0;
    }

    public async Task<int> DomainRenewAsync(string name, string currentExpirationDate, int period = 1)
    {
        var (dom, elem) = CreateApiDocument("domain", "renew");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        elem.AppendChild(dom.CreateElement("currentExpirationDate")).InnerText = currentExpirationDate;
        elem.AppendChild(dom.CreateElement("period")).InnerText = period.ToString();
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var node = response.SelectSingleNode("/api/domain/renew/actionId");
        return node != null ? int.Parse(node.InnerText) : 0;
    }

    public async Task<int?> DomainUpdateAsync(string name, IEnumerable<string> ns)
    {
        var (dom, elem) = CreateApiDocument("domain", "update");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        var nsElem = dom.CreateElement("ns");
        foreach (var n in ns)
        {
            nsElem.AppendChild(dom.CreateElement("ns")).AppendChild(dom.CreateElement("name")).InnerText = n;
        }
        elem.AppendChild(nsElem);
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var node = response.SelectSingleNode("/api/domain/update/actionId");
        if (node == null)
            return null;
        return int.Parse(node.InnerText);
    }

    public async Task<Dictionary<string, string>> DomainCheckAsync(IEnumerable<string> names)
    {
        var (dom, elem) = CreateApiDocument("domain", "check");
        foreach (var n in names)
        {
            elem.AppendChild(dom.CreateElement("name")).InnerText = n;
        }
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var result = new Dictionary<string, string>();
        foreach (XmlNode node in response.SelectNodes("/api/domain/check/name")!)
        {
            var status = node.Attributes?[0]?.InnerText ?? string.Empty;
            result[node.InnerText] = status;
        }
        return result;
    }

    public async IAsyncEnumerable<string> DomainListAsync(string? lastName = null)
    {
        var (dom, elem) = CreateApiDocument("domain", "list");
        if (!string.IsNullOrEmpty(lastName))
        {
            elem.AppendChild(dom.CreateElement("lastName")).InnerText = lastName;
        }
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        foreach (XmlNode node in response.SelectNodes("/api/domain/list/name")!)
        {
            yield return node.InnerText;
        }
    }

    public async Task<int> DomainTradeAsync(string name, int user, string pw)
    {
        var (dom, elem) = CreateApiDocument("domain", "trade");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        elem.AppendChild(dom.CreateElement("user")).InnerText = user.ToString();
        elem.AppendChild(dom.CreateElement("pw")).InnerText = pw;
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var node = response.SelectSingleNode("/api/domain/trade/actionId");
        return node != null ? int.Parse(node.InnerText) : 0;
    }

    public async Task<string?> DomainTradeGetPwAsync(string name)
    {
        var (dom, elem) = CreateApiDocument("domain", "tradeGetPw");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        return response.SelectSingleNode("/api/domain/tradeGetPw/pw")?.InnerText;
    }

    public async Task DomainTransferCancelAsync(string name, int user)
    {
        var (dom, elem) = CreateApiDocument("domain", "transferCancel");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        elem.AppendChild(dom.CreateElement("user")).InnerText = user.ToString();
        await SendAsync(dom).ConfigureAwait(false);
        await ReadAsync().ConfigureAwait(false);
    }

    public async Task DomainEnablePPAsync(string name)
    {
        var (dom, elem) = CreateApiDocument("domain", "enablePP");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        await SendAsync(dom).ConfigureAwait(false);
        await ReadAsync().ConfigureAwait(false);
    }

    public async Task DomainDisablePPAsync(string name)
    {
        var (dom, elem) = CreateApiDocument("domain", "disablePP");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        await SendAsync(dom).ConfigureAwait(false);
        await ReadAsync().ConfigureAwait(false);
    }

    public async Task DomainBuyPPAsync(string name)
    {
        var (dom, elem) = CreateApiDocument("domain", "buyPP");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        await SendAsync(dom).ConfigureAwait(false);
        await ReadAsync().ConfigureAwait(false);
    }

    public async Task<Dictionary<string, string>?> DomainTradeStatusAsync(int id)
    {
        var (dom, elem) = CreateApiDocument("domain", "tradeStatus");
        elem.AppendChild(dom.CreateElement("id")).InnerText = id.ToString();
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var nodes = response.SelectNodes("/api/domain/tradeStatus/*");
        if (nodes == null || nodes.Count == 0)
            return null;
        var result = new Dictionary<string, string>();
        foreach (XmlNode node in nodes)
        {
            result[node.Name] = node.InnerText;
        }
        return result;
    }

    public async Task<string?> DomainWhoisAsync(string name)
    {
        var (dom, elem) = CreateApiDocument("domain", "whois");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        return response.SelectSingleNode("/api/domain/whois/textResponse")?.InnerText;
    }

    public async Task<int?> DomainPrivacySetAsync(string name, bool privacy)
    {
        var (dom, elem) = CreateApiDocument("domain", "privacySet");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        elem.AppendChild(dom.CreateElement("privacy")).InnerText = privacy ? "true" : "false";
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var node = response.SelectSingleNode("/api/domain/privacySet/actionId");
        if (node == null)
            return null;
        return int.Parse(node.InnerText);
    }

    public async Task<Dictionary<string, string>?> DomainDnsInfoAsync(string name)
    {
        var (dom, elem) = CreateApiDocument("domain", "dnsInfo");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var nodes = response.SelectNodes("/api/domain/dnsInfo/*");
        if (nodes == null || nodes.Count == 0)
            return null;
        var result = new Dictionary<string, string>();
        foreach (XmlNode node in nodes)
        {
            result[node.Name] = node.InnerText;
        }
        return result;
    }

    public async Task DomainDnsChangeAsync(string name, Dictionary<string, string> rrset)
    {
        var (dom, elem) = CreateApiDocument("domain", "dnsChange");
        elem.AppendChild(dom.CreateElement("name")).InnerText = name;
        var rrsetElem = dom.CreateElement("rrset");
        foreach (var kv in rrset)
        {
            rrsetElem.AppendChild(dom.CreateElement(kv.Key)).InnerText = kv.Value;
        }
        elem.AppendChild(rrsetElem);
        await SendAsync(dom).ConfigureAwait(false);
        await ReadAsync().ConfigureAwait(false);
    }

    public async Task<Dictionary<string, string>?> NewsInfoAsync(int id)
    {
        var (dom, elem) = CreateApiDocument("news", "info");
        elem.AppendChild(dom.CreateElement("id")).InnerText = id.ToString();
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var nodes = response.SelectNodes("/api/news/info/*");
        if (nodes == null || nodes.Count == 0)
            return null;
        var result = new Dictionary<string, string>();
        foreach (XmlNode node in nodes)
        {
            result[node.Name] = node.InnerText;
        }
        return result;
    }

    public async IAsyncEnumerable<int> NewsListAsync(int lastId = 0)
    {
        var (dom, elem) = CreateApiDocument("news", "list");
        if (lastId > 0)
        {
            elem.AppendChild(dom.CreateElement("lastId")).InnerText = lastId.ToString();
        }
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        foreach (XmlNode node in response.SelectNodes("/api/news/list/id")!)
        {
            yield return int.Parse(node.InnerText);
        }
    }

    public async Task<Dictionary<string, string>?> UserInfoAsync(int id)
    {
        var (dom, elem) = CreateApiDocument("user", "info");
        elem.AppendChild(dom.CreateElement("id")).InnerText = id.ToString();
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var nodes = response.SelectNodes("/api/user/info/*");
        if (nodes == null || nodes.Count == 0)
            return null;
        var result = new Dictionary<string, string>();
        foreach (XmlNode node in nodes)
        {
            result[node.Name] = node.InnerText;
        }
        return result;
    }

    public async Task<int> UserCreateAsync(Dictionary<string, string> data)
    {
        var (dom, elem) = CreateApiDocument("user", "create");
        foreach (var kv in data)
        {
            elem.AppendChild(dom.CreateElement(kv.Key)).InnerText = kv.Value;
        }
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        var node = response.SelectSingleNode("/api/user/create/id");
        return node != null ? int.Parse(node.InnerText) : 0;
    }

    public async IAsyncEnumerable<int> ActionListAsync(int lastId = 0)
    {
        var (dom, elem) = CreateApiDocument("action", "list");
        if (lastId > 0)
        {
            elem.AppendChild(dom.CreateElement("lastId")).InnerText = lastId.ToString();
        }
        await SendAsync(dom).ConfigureAwait(false);
        var response = await ReadAsync().ConfigureAwait(false);
        foreach (XmlNode node in response.SelectNodes("/api/action/list/id")!)
        {
            yield return int.Parse(node.InnerText);
        }
    }


    private async Task EnsureConnectionAsync()
    {
        if (_stream != null)
            return;

        var client = new TcpClient();
        client.ReceiveTimeout = (int)_timeout.TotalMilliseconds;
        client.SendTimeout = (int)_timeout.TotalMilliseconds;
        await client.ConnectAsync(_host, _port).ConfigureAwait(false);
        _stream = new SslStream(client.GetStream(), false);
        await _stream.AuthenticateAsClientAsync(_host).ConfigureAwait(false);
    }

    private async Task SendAsync(XmlDocument xml)
    {
        await EnsureConnectionAsync().ConfigureAwait(false);
        var data = Encoding.UTF8.GetBytes(xml.OuterXml);
        using var sha = SHA512.Create();
        var hash = sha.ComputeHash(Combine(data, _hash));
        var payload = Combine(hash, data);
        var length = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(payload.Length));
        await _stream!.WriteAsync(length).ConfigureAwait(false);
        await _stream.WriteAsync(payload).ConfigureAwait(false);
    }

    private async Task<XmlDocument> ReadAsync()
    {
        var lengthBuffer = new byte[4];
        await ReadExactAsync(lengthBuffer).ConfigureAwait(false);
        int length = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
        var buffer = new byte[length];
        await ReadExactAsync(buffer).ConfigureAwait(false);
        var xml = Encoding.UTF8.GetString(buffer);
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc;
    }

    private async Task ReadExactAsync(byte[] buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await _stream!.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset)).ConfigureAwait(false);
            if (read == 0)
                throw new IOException("unexpected end of stream");
            offset += read;
        }
    }

    private static byte[] Combine(byte[] a, byte[] b)
    {
        var result = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, result, 0, a.Length);
        Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
        return result;
    }

    private static (XmlDocument dom, XmlElement elem) CreateApiDocument(string name1, string? name2 = null)
    {
        var dom = new XmlDocument();
        var api = dom.CreateElement("api", "http://api.hrd.pl/api/");
        var elem1 = dom.CreateElement(name1);
        XmlElement elem2;
        if (name2 != null)
        {
            elem2 = dom.CreateElement(name2);
            elem1.AppendChild(elem2);
        }
        else
        {
            elem2 = elem1;
        }
        api.AppendChild(elem1);
        dom.AppendChild(api);
        return (dom, elem2);
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream != null)
        {
            await _stream.DisposeAsync();
        }
    }
}
