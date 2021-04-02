using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sora.OnebotModel.OnebotEvent.MessageEvent;
using Sora.Entities;
using Sora.Entities.Info;
using Sora.Entities.MessageElement;
using Sora.Enumeration;
using Sora.Enumeration.ApiType;
using Sora.OnebotModel;
using Group = Sora.Entities.Group;

namespace Sora.EventArgs.SoraEvent
{
    /// <summary>
    /// 群消息事件参数
    /// </summary>
    public sealed class GroupMessageEventArgs : BaseSoraEventArgs
    {
        #region 属性

        /// <summary>
        /// 消息内容
        /// </summary>
        public Message Message { get; }

        /// <summary>
        /// 是否来源于匿名群成员
        /// </summary>
        public bool IsAnonymousMessage { get; }

        /// <summary>
        /// 是否为Bot账号所发送的消息
        /// </summary>
        public bool IsSelfMessage { get; }

        /// <summary>
        /// 消息发送者实例
        /// </summary>
        public User Sender { get; }

        /// <summary>
        /// 发送者信息
        /// </summary>
        public GroupSenderInfo SenderInfo { get; }

        /// <summary>
        /// 消息来源群组实例
        /// </summary>
        public Group SourceGroup { get; }

        /// <summary>
        /// 匿名用户实例
        /// </summary>
        public Anonymous Anonymous { get; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="connectionGuid">服务器链接标识</param>
        /// <param name="eventName">事件名</param>
        /// <param name="groupMsgArgs">群消息事件参数</param>
        internal GroupMessageEventArgs(Guid connectionGuid, string eventName, ApiGroupMsgEventArgs groupMsgArgs)
            : base(connectionGuid, eventName, groupMsgArgs.SelfID, groupMsgArgs.Time)
        {
            IsAnonymousMessage = groupMsgArgs.Anonymous != null;
            IsSelfMessage      = groupMsgArgs.MessageType.Equals("group_self");
            //将api消息段转换为CQ码
            Message = new Message(connectionGuid, groupMsgArgs.MessageId, groupMsgArgs.RawMessage,
                                  MessageParse.Parse(groupMsgArgs.MessageList), groupMsgArgs.Time,
                                  groupMsgArgs.Font, groupMsgArgs.MessageSequence);
            Sender      = new User(connectionGuid, groupMsgArgs.UserId);
            SourceGroup = new Group(connectionGuid, groupMsgArgs.GroupId);
            SenderInfo  = groupMsgArgs.SenderInfo;
            Anonymous   = IsAnonymousMessage ? groupMsgArgs.Anonymous : null;
        }

        #endregion

        #region 快捷方法

        /// <summary>
        /// 快速回复
        /// </summary>
        /// <param name="message">
        /// <para>消息</para>
        /// <para>可以为<see cref="string"/>/<see cref="CQCode"/>/<see cref="List{T}"/>(T = <see cref="CQCode"/>)</para>
        /// <para>其他类型的消息会被强制转换为纯文本</para>
        /// </param>
        /// <returns>
        /// <para><see cref="APIStatusType"/> API执行状态</para>
        /// <para><see langword="messageId"/> 发送消息的id</para>
        /// </returns>
        public async ValueTask<(APIStatusType apiStatus, int messageId)> Reply(params object[] message)
        {
            return await SoraApi.SendGroupMessage(SourceGroup.Id, message);
        }

        /// <summary>
        /// 没什么用的复读功能
        /// </summary>
        /// <returns>
        /// <para><see cref="APIStatusType"/> API执行状态</para>
        /// <para><see langword="messageId"/> 发送消息的id</para>
        /// </returns>
        public async ValueTask<(APIStatusType apiStatus, int messageId)> Repeat()
        {
            return await SoraApi.SendGroupMessage(SourceGroup.Id, Message.MessageBody);
        }

        /// <summary>
        /// 撤回发送者消息
        /// 只有在管理员以上权限才有效
        /// </summary>
        public async ValueTask RecallSourceMessage()
        {
            await SoraApi.RecallMessage(Message.MessageId);
        }

        /// <summary>
        /// 获取发送者群成员信息
        /// </summary>
        /// <param name="useCache">是否使用缓存</param>
        /// <returns>
        /// <para><see cref="APIStatusType"/> API执行状态</para>
        /// <para><see cref="GroupMemberInfo"/> 群成员信息</para>
        /// </returns>
        public async ValueTask<(APIStatusType apiStatus, GroupMemberInfo memberInfo)> GetSenderMemberInfo(
            bool useCache = true)
        {
            return await SoraApi.GetGroupMemberInfo(SourceGroup.Id, Sender.Id, useCache);
        }

        #endregion

        #region 连续对话

        /// <summary>
        /// 等待下一条消息触发
        /// </summary>
        /// <param name="commandExps">指令表达式</param>
        /// <param name="matchType">匹配类型</param>
        /// <param name="regexOptions">正则匹配选项</param>
        /// <returns>触发后的事件参数</returns>
        public ValueTask<GroupMessageEventArgs> WaitForNextMessageAsync(string[] commandExps, MatchType matchType,
                                                                        RegexOptions regexOptions = RegexOptions.None)
        {
            return ValueTask.FromResult((GroupMessageEventArgs) WaitForNextMessage(Sender, commandExps, matchType,
                                            regexOptions, SourceGroup));
        }

        #endregion
    }
}