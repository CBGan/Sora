using Newtonsoft.Json;
using Sora.Converter;

namespace Sora.Entities.MessageElement.CQModel
{
    /// <summary>
    /// <para>群成员戳一戳</para>
    /// <para>仅发送</para>
    /// <para>仅支持Go</para>
    /// </summary>
    public struct Poke
    {
        #region 属性

        /// <summary>
        /// 需要戳的成员
        /// </summary>
        [JsonConverter(typeof(StringConverter))]
        [JsonProperty(PropertyName = "qq")]
        public long Uid { get; internal set; }

        #endregion
    }
}