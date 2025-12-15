using System;
using System.Collections.Generic;
using System.Xml.Linq;
using HRD.Api.Exceptions;

namespace HRD.Api
{
    public partial class HRDApi
    {
        // Already implemented in HRDApi.cs:
        // PartnerGetBalance

        public IEnumerable<Dictionary<string, object>> PartnerGetPricings()
        {
            var (doc, elem) = GetApiDocument("partner", "getPricings");
            Send(doc);
            var response = Read();

            // Expected: /api/partner/pricings/pricing
            var pricingsElem = response.Element("api")?.Element("partner")?.Element("pricings");
            if (pricingsElem == null)
            {
                CheckError(response);
                yield break;
            }

            foreach (var pricing in pricingsElem.Elements("pricing"))
            {
                var result = new Dictionary<string, object>();
                result["name"] = pricing.Element("name")?.Value;

                var pricesDict = new Dictionary<string, string>();
                var pricesElem = pricing.Element("prices");

                if (pricesElem != null)
                {
                    foreach (var price in pricesElem.Elements("price"))
                    {
                        var name = price.Element("name")?.Value;
                        var val = price.Element("value")?.Value;
                        if (val == "(brak)") val = null;
                        if (!string.IsNullOrEmpty(name))
                        {
                            pricesDict[name] = val;
                        }
                    }
                }
                result["prices"] = pricesDict;

                yield return result;
            }
        }

        public IEnumerable<string> PartnerGetPricingsList()
        {
            var (doc, elem) = GetApiDocument("partner", "getPricingsList");
            Send(doc);
            var response = Read();

            var pricingsElem = response.Element("api")?.Element("partner")?.Element("pricings");
            if (pricingsElem == null)
            {
                CheckError(response);
                yield break;
            }

            foreach (var p in pricingsElem.Elements("name"))
            {
                yield return p.Value;
            }
        }

        public Dictionary<string, object> PartnerPricingServiceInfo(string name)
        {
            // TODO: Check implementation of partnerPricingServiceInfo
            // It seems similar to others.
            // But let's skip complex pricing methods for now to focus on core features if file size limited
            // Actually I will implement it.

            var (doc, elem) = GetApiDocument("partner", "getPricingInfo");
            elem.Add(new XElement(_ns + "name", name));

            Send(doc);
            var response = Read();

            // Output similar to PartnerGetPricings but single item?
            // PHP: return $this->sendArrayResponse($dom, 'partner/getPricingInfo/*');
            // It parses `pricing` node.
            // Let's implement generic ParsePricing if needed.
            // For now return raw dict.

            var info = response.Element("api")?.Element("partner")?.Element("getPricingInfo");
            if (info == null)
            {
                CheckError(response);
                throw new HRDApiCommunicationException("Empty response");
            }

            // Manually parse for better control or map to Model
            var result = new Dictionary<string, object>();
            result["name"] = info.Element("name")?.Value;

            var pricesDict = new Dictionary<string, string>();
            var pricesElem = info.Element("prices");
            if (pricesElem != null)
            {
                foreach (var price in pricesElem.Elements("price"))
                {
                    var pName = price.Element("name")?.Value;
                    var pVal = price.Element("value")?.Value;
                    if (pVal == "(brak)") pVal = null;
                    if (!string.IsNullOrEmpty(pName))
                        pricesDict[pName] = pVal;
                }
            }
            result["prices"] = pricesDict;

            return result;
        }
    }
}
