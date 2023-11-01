using Newtonsoft.Json;

namespace GisRouteApi.Models
{
    public class Answere<T> : AnswereBasic
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public T Data { get; set; }

        public Answere()
        {
            base.AnswereId = 0L;
            base.AnswereMessage = "default";
            base.AnswereComment = "";
        }

        public Answere(long inAnswereId, string inAnswereMessage, string inAnswereComment)
        {
            base.AnswereId = inAnswereId;
            base.AnswereMessage = inAnswereMessage;
            base.AnswereComment = inAnswereComment;
        }

        public Answere(long inAnswereId, string inAnswereMessage)
        {
            base.AnswereId = inAnswereId;
            base.AnswereMessage = (base.AnswereComment = inAnswereMessage);
        }

        public Answere(long inAnswereId, string inAnswereMessage, string inAnswereComment, T inData)
        {
            base.AnswereId = inAnswereId;
            base.AnswereMessage = inAnswereMessage;
            base.AnswereComment = inAnswereComment;
            Data = inData;
        }

        public Answere(T inData)
        {
            base.AnswereId = 1L;
            base.AnswereMessage = "Ok";
            base.AnswereComment = "";
            Data = inData;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.None, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }
    }
}
