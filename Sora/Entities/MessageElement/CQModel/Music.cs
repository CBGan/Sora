using Newtonsoft.Json;
using Sora.Converter;
using Sora.Enumeration.EventParamsType;

namespace Sora.Entities.MessageElement.CQModel
{
    /// <summary>
    /// 音乐分享
    /// 仅发送
    /// </summary>
    public struct Music
    {
        /// <summary>
        /// 音乐分享类型
        /// </summary>
        [JsonConverter(typeof(EnumDescriptionConverter))]
        [JsonProperty(PropertyName = "type")]
        public MusicShareType MusicType { get; internal set; }

        /// <summary>
        /// 歌曲 ID
        /// </summary>
        [JsonConverter(typeof(StringConverter))]
        [JsonProperty(PropertyName = "id")]
        public long MusicId { get; internal set; }
    }
}