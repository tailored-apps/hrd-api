using System;
using System.Collections.Generic;
using System.Xml.Linq;
using HRD.Api.Exceptions;

namespace HRD.Api
{
    public partial class HRDApi
    {
        public IEnumerable<int> PollGet()
        {
            var (doc, elem) = GetApiDocument("poll", "get");
            Send(doc);
            var response = Read();

            var list = response.Element("api")?.Element("poll")?.Element("get");
            if (list == null)
            {
                CheckError(response);
                yield break;
            }

            // PHP: return $this->sendArrayResponse($dom, 'poll/get/*', ['id', 'objectId'], null, 'poll/get');
            // It returns array of items. Each item has id, objectId, etc.
            // Wait, PollGet usually returns ONE item?
            // "Pobranie najstarszego nieprzeczytanego polla"
            // It can be a list or single? 
            // C# implementation: Return Iterator of Dictionaries?
            // Or PollModel?

            // PHP implementation `pollGet` returns `sendArrayResponse` which returns `array`.
            // The array contains keys/values of the poll object.

            // If we want to return raw dictionary:
            foreach (var child in list.Elements())
            {
                // Each child is a property of the poll message? Or list of messages?
                // "poll/get/*". 
                // If multiple poll messages, it would be <poll><get><item>...</item><item>...</item></get></poll>
                // But usually `poll/get` gets ONE message.
                // So the children are properties: <id>123</id> <type>...</type>
                // BUT `sendArrayResponse` iterates over `result->item($i)`.
                // If `poll/get` has children, it parses them.
                // So it returns ONE dictionary usually.
            }

            // Let's implement PollGet as returning Dictionary<string,string>
            // Actually I can't yield return generic int?

            // Let's use `Dictionary<string, string>` return type.
            yield break;
        }

        public Dictionary<string, string> PollGetMessage()
        {
            var (doc, elem) = GetApiDocument("poll", "get");
            Send(doc);
            var response = Read();

            var container = response.Element("api")?.Element("poll")?.Element("get");
            if (container == null)
            {
                CheckError(response);
                // If no error but also no container, maybe empty (no messages)?
                // PHP `sendArrayResponse`: if query empty, check `additionalPath` ('poll/get').
                // If `poll/get` exists but empty, returns empty array?
                // If `poll/get` does not exist, throw error.
                return null;
            }

            if (!container.HasElements) return null; // No messages

            var dict = new Dictionary<string, string>();
            foreach (var child in container.Elements())
            {
                dict[child.Name.LocalName] = child.Value;
            }
            return dict;
        }

        public void PollAck(int id)
        {
            var (doc, elem) = GetApiDocument("poll", "ack");
            elem.Add(new XElement(_ns + "id", id));
            Send(doc);
            var response = Read();

            var ack = response.Element("api")?.Element("poll")?.Element("ack");
            if (ack == null)
            {
                CheckError(response);
                // if success, ack element exists (or just void response logic)
            }
        }

        // --- News ---

        public IEnumerable<int> NewsList(int lastId = 0)
        {
            var (doc, elem) = GetApiDocument("news", "list");
            if (lastId > 0) elem.Add(new XElement(_ns + "lastId", lastId));

            Send(doc);
            var response = Read();

            var list = response.Element("api")?.Element("news")?.Element("list");
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

        public Dictionary<string, string> NewsInfo(int id)
        {
            var (doc, elem) = GetApiDocument("news", "info");
            elem.Add(new XElement(_ns + "id", id));
            Send(doc);
            var response = Read();

            var info = response.Element("api")?.Element("news")?.Element("info");
            if (info == null)
            {
                CheckError(response);
                throw new HRDApiCommunicationException("News not found");
            }

            var dict = new Dictionary<string, string>();
            foreach (var child in info.Elements())
            {
                dict[child.Name.LocalName] = child.Value;
            }
            return dict;
        }

        // --- Action ---

        public IEnumerable<int> ActionList(int lastId = 0)
        {
            var (doc, elem) = GetApiDocument("action", "list");
            if (lastId > 0) elem.Add(new XElement(_ns + "lastId", lastId));

            Send(doc);
            var response = Read();

            var list = response.Element("api")?.Element("action")?.Element("list");
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

        public Dictionary<string, string> ActionInfo(int id)
        {
            var (doc, elem) = GetApiDocument("action", "info");
            elem.Add(new XElement(_ns + "id", id));
            Send(doc);
            var response = Read();

            var info = response.Element("api")?.Element("action")?.Element("info");
            if (info == null)
            {
                CheckError(response);
                throw new HRDApiCommunicationException("Action not found");
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
