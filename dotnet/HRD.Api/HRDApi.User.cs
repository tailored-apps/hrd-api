using System;
using System.Collections.Generic;
using System.Xml.Linq;
using HRD.Api.Exceptions;

namespace HRD.Api
{
    public partial class HRDApi
    {
        public IEnumerable<int> UserList(int lastId = 0)
        {
            var (doc, elem) = GetApiDocument("user", "list");
            if (lastId > 0) elem.Add(new XElement(_ns + "lastId", lastId));

            Send(doc);
            var response = Read();

            var list = response.Element("api")?.Element("user")?.Element("list");
            if (list == null)
            {
                CheckError(response);
                yield break;
            }

            foreach (var idElem in list.Elements("id"))
            {
                if (int.TryParse(idElem.Value, out int id)) yield return id;
            }
        }

        public Dictionary<string, string> UserInfo(int id)
        {
            var (doc, elem) = GetApiDocument("user", "info");
            elem.Add(new XElement(_ns + "id", id));

            Send(doc);
            var response = Read();

            var info = response.Element("api")?.Element("user")?.Element("info");
            if (info == null)
            {
                CheckError(response);
                throw new HRDApiCommunicationException("User not found");
            }

            var dict = new Dictionary<string, string>();
            foreach (var child in info.Elements())
            {
                dict[child.Name.LocalName] = child.Value;
            }
            return dict;
        }

        // Helper specifically for UserCreate because it has many params
        public int UserCreate(
            string type,
            string idNumber,
            string email,
            string mobilePhone,
            string landlinePhone,
            string fax,
            string name,
            string street,
            string postcode,
            string city,
            string country,
            string representative = null
        )
        {
            var (doc, elem) = GetApiDocument("user", "create");

            // type: person|company|owner
            switch (type)
            {
                case "person":
                    elem.Add(new XElement(_ns + "personType"));
                    break;
                case "company":
                    elem.Add(new XElement(_ns + "companyType"));
                    if (representative != null) elem.Add(new XElement(_ns + "representative", representative));
                    break;
                case "owner":
                    elem.Add(new XElement(_ns + "ownerType"));
                    if (representative != null) elem.Add(new XElement(_ns + "representative", representative));
                    break;
                default:
                    throw new HRDApiIncorrectDataException("invalid type");
            }

            elem.Add(new XElement(_ns + "idNumber", idNumber));
            elem.Add(new XElement(_ns + "email", email));
            if (!string.IsNullOrEmpty(mobilePhone)) elem.Add(new XElement(_ns + "mobilePhone", mobilePhone));
            elem.Add(new XElement(_ns + "landlinePhone", landlinePhone));
            if (!string.IsNullOrEmpty(fax)) elem.Add(new XElement(_ns + "fax", fax));

            elem.Add(new XElement(_ns + "name", name));
            elem.Add(new XElement(_ns + "street", street));
            elem.Add(new XElement(_ns + "postcode", postcode));
            elem.Add(new XElement(_ns + "city", city));
            elem.Add(new XElement(_ns + "country", country));

            Send(doc);
            var response = Read();

            // response path: user/create/id
            var idElem = response.Element("api")?.Element("user")?.Element("create")?.Element("id");
            if (idElem != null && int.TryParse(idElem.Value, out int newId))
            {
                return newId;
            }
            CheckError(response);
            throw new HRDApiCommunicationException("Failed to get Created User ID");
        }
    }
}
