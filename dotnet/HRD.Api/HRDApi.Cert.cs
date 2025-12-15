using System;
using System.Collections.Generic;
using System.Xml.Linq;
using HRD.Api.Exceptions;

namespace HRD.Api
{
    public partial class HRDApi
    {
        public IEnumerable<string> CertList(int? lastId = null)
        {
            var (doc, elem) = GetApiDocument("cert", "list");
            if (lastId.HasValue) elem.Add(new XElement(_ns + "lastId", lastId.Value));

            Send(doc);
            var response = Read();

            var list = response.Element("api")?.Element("cert")?.Element("list");
            if (list == null)
            {
                CheckError(response);
                yield break;
            }

            foreach (var idElem in list.Elements("id"))
            {
                yield return idElem.Value;
            }
        }

        public Dictionary<string, string> CertInfo(int id)
        {
            var (doc, elem) = GetApiDocument("cert", "info");
            elem.Add(new XElement(_ns + "id", id));

            Send(doc);
            var response = Read();

            var info = response.Element("api")?.Element("cert")?.Element("info");
            if (info == null)
            {
                CheckError(response);
                throw new HRDApiCommunicationException("Cert not found");
            }

            var dict = new Dictionary<string, string>();
            foreach (var child in info.Elements())
            {
                dict[child.Name.LocalName] = child.Value;
            }
            return dict;
        }

        public Dictionary<string, string> CertContactInfo(int id)
        {
            var (doc, elem) = GetApiDocument("cert", "contactInfo");
            elem.Add(new XElement(_ns + "id", id));

            Send(doc);
            var response = Read();

            var info = response.Element("api")?.Element("cert")?.Element("contactInfo");
            if (info == null)
            {
                CheckError(response);
                throw new HRDApiCommunicationException("Cert Contact not found");
            }

            var dict = new Dictionary<string, string>();
            foreach (var child in info.Elements())
            {
                dict[child.Name.LocalName] = child.Value;
            }
            return dict;
        }
    }
}
