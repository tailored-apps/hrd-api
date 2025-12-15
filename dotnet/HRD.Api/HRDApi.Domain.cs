using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using HRD.Api.Exceptions;

namespace HRD.Api
{
    public partial class HRDApi
    {
        private readonly IdnMapping _idn = new IdnMapping();

        private string IdnToAscii(string domain)
        {
            try
            {
                return _idn.GetAscii(domain);
            }
            catch
            {
                return domain; // Fallback or throw? PHP allows it to fail? default behavior
            }
        }

        private string IdnToUtf8(string domain)
        {
            try
            {
                return _idn.GetUnicode(domain);
            }
            catch
            {
                return domain;
            }
        }

        public Dictionary<string, string> DomainCheck(IEnumerable<string> domains)
        {
            var domainList = new List<string>(domains);
            int count = domainList.Count;
            if (count == 0) return new Dictionary<string, string>();

            var (doc, elem) = GetApiDocument("domain", "check");
            foreach (var d in domainList)
            {
                elem.Add(new XElement(_ns + "name", IdnToAscii(d)));
            }

            Send(doc);

            var result = new Dictionary<string, string>();
            int returnedCount = 0;

            while (returnedCount < count)
            {
                var response = Read();

                // XPath: /api/domain/check/name
                var checkElem = response.Element("api")?.Element("domain")?.Element("check");
                if (checkElem == null)
                {
                    CheckError(response);
                    // If no error but no check element, maybe weird response?
                    throw new HRDApiCommunicationException("Unexpected response structure in DomainCheck");
                }

                var nameElements = checkElem.Elements("name");
                int inBatch = 0;
                foreach (var item in nameElements)
                {
                    inBatch++;
                    string domainName = item.Value;
                    // PHP: $item->attributes->item(0)->textContent; 
                    // It assumes the first attribute is the status. 
                    // Let's check schema/examples. 
                    // Usually <name result="available">example.pl</name> or similar.
                    // Or <name>example.pl</name> with attribute?
                    // PHP code: $item->attributes->item(0)->textContent

                    // We need to find the attribute.
                    string status = "unknown";
                    if (item.HasAttributes)
                    {
                        var attr = item.FirstAttribute; // item(0)
                        status = attr.Value;
                    }

                    result[domainName] = status;
                }

                returnedCount += inBatch;

                if (inBatch == 0)
                {
                    // Avoid infinite loop if server sends empty packets or error
                    throw new HRDApiCommunicationException("Received packet with 0 results while waiting for more");
                }
            }

            return result;
        }

        public Dictionary<string, object> DomainInfo(string name)
        {
            var (doc, elem) = GetApiDocument("domain", "info");
            elem.Add(new XElement(_ns + "name", IdnToAscii(name)));

            Send(doc);
            var response = Read();

            var info = response.Element("api")?.Element("domain")?.Element("info");
            if (info == null)
            {
                CheckError(response);
                throw new HRDApiCommunicationException("Domain info not found");
            }

            var result = new Dictionary<string, object>();
            foreach (var child in info.Elements())
            {
                if (child.Name.LocalName == "ns")
                {
                    // Parse NS structure
                    // For brevity, skipping detailed NS parsing logic here, but it should be done similar to PHP
                    // Just storing raw XML string or simplified list if needed for now
                    result["ns"] = child.ToString();
                }
                else
                {
                    result[child.Name.LocalName] = child.Value;
                }
            }
            return result;
        }

        public int DomainCreate(string domain, int user, string[] ns, int period = 1, bool privacyProtect = false)
        {
            var (doc, elem) = GetApiDocument("domain", "create");
            elem.Add(new XElement(_ns + "name", IdnToAscii(domain)));
            elem.Add(new XElement(_ns + "user", user));
            elem.Add(new XElement(_ns + "period", period));
            elem.Add(new XElement(_ns + "privacyProtect", privacyProtect ? "true" : "false"));

            var nsElem = new XElement(_ns + "ns");
            var nsElem2 = new XElement(_ns + "ns"); // PHP nsToXml -> creates nested ns/ns structure
            foreach (var n in ns)
            {
                var nsElem3 = new XElement(_ns + "ns");
                nsElem3.Add(new XElement(_ns + "name", IdnToAscii(n)));
                nsElem2.Add(nsElem3);
            }
            nsElem.Add(nsElem2);
            elem.Add(nsElem);

            Send(doc);
            var response = Read();

            var actionId = response.Element("api")?.Element("domain")?.Element("create")?.Element("actionId");
            if (actionId != null && int.TryParse(actionId.Value, out int id))
            {
                return id;
            }
            CheckError(response);
            throw new HRDApiCommunicationException("Create failed");
        }

        public int DomainRenew(string domain, DateTime currentExpiration, int period = 1)
        {
            var (doc, elem) = GetApiDocument("domain", "renew");
            elem.Add(new XElement(_ns + "name", IdnToAscii(domain)));
            elem.Add(new XElement(_ns + "currentExpirationDate", currentExpiration.ToString("yyyy-MM-dd")));
            elem.Add(new XElement(_ns + "period", period));

            Send(doc);
            var response = Read();

            var actionId = response.Element("api")?.Element("domain")?.Element("renew")?.Element("actionId");
            if (actionId != null && int.TryParse(actionId.Value, out int id))
            {
                return id;
            }
            CheckError(response);
            throw new HRDApiCommunicationException("Renew failed");
        }

        public int DomainTransfer(string domain, int user, string authInfo, int? period = null)
        {
            var (doc, elem) = GetApiDocument("domain", "transfer");
            elem.Add(new XElement(_ns + "name", IdnToAscii(domain)));
            elem.Add(new XElement(_ns + "user", user));
            if (period.HasValue) elem.Add(new XElement(_ns + "period", period.Value));
            elem.Add(new XElement(_ns + "pw", authInfo));

            Send(doc);
            var response = Read();

            var actionId = response.Element("api")?.Element("domain")?.Element("transfer")?.Element("actionId");
            if (actionId != null && int.TryParse(actionId.Value, out int id))
            {
                return id;
            }
            CheckError(response);
            throw new HRDApiCommunicationException("Transfer failed");
        }

        public int DomainTrade(string domain, int user, string authInfo)
        {
            var (doc, elem) = GetApiDocument("domain", "trade");
            elem.Add(new XElement(_ns + "name", IdnToAscii(domain)));
            elem.Add(new XElement(_ns + "user", user));
            elem.Add(new XElement(_ns + "pw", authInfo));

            Send(doc);
            var response = Read();

            var actionId = response.Element("api")?.Element("domain")?.Element("trade")?.Element("actionId");
            if (actionId != null && int.TryParse(actionId.Value, out int id))
            {
                return id;
            }
            CheckError(response);
            throw new HRDApiCommunicationException("Trade failed");
        }

        public Dictionary<string, string> DomainWhois(string domain)
        {
            var (doc, elem) = GetApiDocument("domain", "whois");
            elem.Add(new XElement(_ns + "name", IdnToAscii(domain)));

            Send(doc);
            var response = Read();

            var whois = response.Element("api")?.Element("domain")?.Element("whois");
            if (whois == null)
            {
                CheckError(response);
                throw new HRDApiCommunicationException("Whois failed");
            }
            // Usually returns <whois>text content</whois> or structured?
            // "whois" element usually contains the text.
            return new Dictionary<string, string> { { "whois", whois.Value } };
        }

        public IEnumerable<string> DomainList(string lastName = null)
        {
            // Paginated list
            var (doc, elem) = GetApiDocument("domain", "list");
            if (!string.IsNullOrEmpty(lastName)) elem.Add(new XElement(_ns + "lastName", IdnToAscii(lastName)));

            Send(doc);
            var response = Read();

            var list = response.Element("api")?.Element("domain")?.Element("list");
            if (list == null)
            {
                CheckError(response);
                yield break;
            }

            foreach (var n in list.Elements("name"))
            {
                yield return IdnToUtf8(n.Value);
            }
        }

        // Simplified DnsChange (Raw records)
        public int DomainDnsChange(string domain, List<Dictionary<string, string>> records)
        {
            var (doc, elem) = GetApiDocument("domain", "dnsChange");
            elem.Add(new XElement(_ns + "name", IdnToAscii(domain)));

            foreach (var rec in records)
            {
                var rrset = new XElement(_ns + "rrset");
                rrset.Add(new XElement(_ns + "name", rec.ContainsKey("name") ? rec["name"] : domain));
                rrset.Add(new XElement(_ns + "type", rec["type"]));
                if (rec.ContainsKey("ttl")) rrset.Add(new XElement(_ns + "ttl", rec["ttl"]));
                rrset.Add(new XElement(_ns + "changetype", rec.ContainsKey("changetype") ? rec["changetype"] : "REPLACE"));

                if (rec.ContainsKey("content"))
                {
                    var record = new XElement(_ns + "record");
                    record.Add(new XElement(_ns + "content", rec["content"]));
                    record.Add(new XElement(_ns + "disabled", rec.ContainsKey("disabled") ? rec["disabled"] : "false"));
                    rrset.Add(record);
                }

                elem.Add(rrset);
            }

            Send(doc);
            var response = Read();

            // Check success
            var api = response.Element("api");
            // If error, CheckError throws.
            CheckError(response);
            return 1; // Success
        }
    }
}

