using System;
using Sora.OnebotModel.OnebotEvent.NoticeEvent;
using Sora.Entities;

namespace Sora.EventArgs.SoraEvent
{
    /// <summary>
    /// 红包运气王事件参数
    /// </summary>
    public sealed class LuckyKingEventArgs : BaseSoraEventArgs
    {
        #region 属性

        /// <summary>
        /// 红包发送者
        /// </summary>
        public User SendUser { get; private set; }

        /// <summary>
        /// 运气王
        /// </summary>
        public User TargetUser { get; private set; }

        /// <summary>
        /// 消息源群
        /// </summary>
        public Group SourceGroup { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="connectionGuid">服务器链接标识</param>
        /// <param name="eventName">事件名</param>
        /// <param name="luckyKingEventArgs">运气王事件参数</param>
        internal LuckyKingEventArgs(Guid connectionGuid, string eventName, ApiPokeOrLuckyEventArgs luckyKingEventArgs) :
            base(connectionGuid, eventName, luckyKingEventArgs.SelfID, luckyKingEventArgs.Time)
        {
            SendUser    = new User(connectionGuid, luckyKingEventArgs.UserId);
            TargetUser  = new User(connectionGuid, luckyKingEventArgs.TargetId);
            SourceGroup = new Group(connectionGuid, luckyKingEventArgs.GroupId);
        }

        #endregion
    }
}