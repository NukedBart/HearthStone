﻿using Engine.Card;
using Engine.Client;
using Engine.Control;
using Engine.Server;
using Engine.Utility;
using System;
using System.Collections.Generic;

namespace Engine.Action
{
    /// <summary>
    /// 执行
    /// </summary>
    public static class RunAction
    {
        /// <summary>
        /// 获目标位置
        /// </summary>
        public static CardUtility.delegateGetPutPos GetPutPos;
        /// <summary>
        /// 开始一个动作
        /// </summary>
        /// <param name="actionStatus">
        /// 在调用这个过程之前，actionStatus里面的本方和对方都应该已经设定好了
        /// 在这里不进行任何的方向判断，默认这个方法是由MyInfo发起的
        /// </param>
        /// <param name="CardSn"></param>
        /// <param name="AIParm">AI决定的参数</param>
        /// <returns></returns>
        public static List<String> StartAction(ActionStatus actionStatus, String CardSn, String[] AIParm = null)
        {
            //清除事件池，注意，事件将在动作结束后整体结算
            actionStatus.eventhandler.事件池.Clear();
            Engine.Card.CardBasicInfo card = Engine.Utility.CardUtility.GetCardInfoBySN(CardSn);
            List<String> ActionCodeLst = new List<string>();
            //未知的异常，卡牌资料缺失
            if (card == null) return ActionCodeLst;
            PublicInfo PlayInfo;
            PlayInfo = actionStatus.AllRole.MyPublicInfo;
            switch (card.卡牌种类)
            {
                case CardBasicInfo.卡牌类型枚举.法术:
                    UseSpell(actionStatus, CardSn, card, ActionCodeLst, PlayInfo);
                    break;
                case CardBasicInfo.卡牌类型枚举.随从:
                    UseMinion(actionStatus, CardSn, AIParm, card, ActionCodeLst, PlayInfo);
                    break;
                case CardBasicInfo.卡牌类型枚举.武器:
                    ActionCodeLst.Add(ActionCode.strWeapon + CardUtility.strSplitMark + CardSn);
                    PlayInfo.Weapon = (Engine.Card.WeaponCard)card;
                    break;
                case CardBasicInfo.卡牌类型枚举.奥秘:
                    ActionCodeLst.Add(ActionCode.strSecret + CardUtility.strSplitMark + CardSn);
                    actionStatus.AllRole.MyPrivateInfo.奥秘列表.Add((Engine.Card.SecretCard)card);
                    PlayInfo.SecretCount = actionStatus.AllRole.MyPrivateInfo.奥秘列表.Count;
                    break;
                default:
                    break;
            }
            //随从卡牌的连击效果启动
            if ((card.卡牌种类 != CardBasicInfo.卡牌类型枚举.法术) &&
                PlayInfo.连击状态 && (!String.IsNullOrEmpty(card.连击效果)))
            {
                //初始化 Buff效果等等
                Engine.Card.SpellCard ablity = (Engine.Card.SpellCard)CardUtility.GetCardInfoBySN(card.连击效果);
                if (ablity != null)
                {
                    var ResultArg = ablity.UseSpell(actionStatus);
                    if (ResultArg.Count != 0)
                    {
                        ActionCodeLst.AddRange(ResultArg);
                        //英雄技能等的时候，不算[本方施法] 
                        if (card.原生卡牌)
                            actionStatus.eventhandler.事件池.Add(new Engine.Utility.CardUtility.全局事件()
                            {
                                触发事件类型 = CardUtility.事件类型枚举.施法,
                                触发位置 = PlayInfo.战场位置
                            });
                    }
                }
            }
            if (ActionCodeLst.Count != 0)
            {
                PlayInfo.连击状态 = true;
                ActionCodeLst.AddRange(actionStatus.eventhandler.事件处理(actionStatus));
            }
            return ActionCodeLst;
        }

        private static void UseMinion(ActionStatus game, String CardSn, String[] AIParm, Engine.Card.CardBasicInfo card, List<String> ActionCodeLst, PublicInfo PlayInfo)
        {
            int MinionPos = 1;
            if (PlayInfo.BattleField.MinionCount != 0)
            {
                if (AIParm == null)
                {
                    MinionPos = GetPutPos(game.AllRole.MyPublicInfo.BattleField);
                }
                else
                {
                    MinionPos = int.Parse(AIParm[0]);
                }
            }
            if (MinionPos != -1)
            {
                ActionCodeLst.Add(ActionCode.strMinion + CardUtility.strSplitMark + CardSn + CardUtility.strSplitMark + MinionPos.ToString("D1"));
                var minion = (Engine.Card.MinionCard)card;
                //初始化
                minion.初始化();
                //必须在放入之前做得原因是，被放入的随从不能被触发这个事件
                game.eventhandler.事件池.Add(new Engine.Utility.CardUtility.全局事件()
                {
                    触发事件类型 = CardUtility.事件类型枚举.召唤,
                    触发位置 = new CardUtility.指定位置结构体()
                    {
                        Postion = MinionPos,
                        本方对方标识 = true
                    }
                });
                switch (minion.战吼类型)
                {
                    case MinionCard.战吼类型枚举.默认:
                        PlayInfo.BattleField.PutToBattle(MinionPos, minion);
                        ActionCodeLst.AddRange(minion.发动战吼(game));
                        break;
                    case MinionCard.战吼类型枚举.抢先:
                        //战吼中，其他系列的法术效果 例如其他鱼人获得XX效果
                        //战吼中，友方系列的法术效果 例如友方随从获得XX效果
                        foreach (var result in minion.发动战吼(game))
                        {
                            var resultArray = result.Split(CardUtility.strSplitMark.ToCharArray());
                            if (resultArray.Length == 1 || int.Parse(resultArray[2]) < MinionPos)
                            {
                                //SETTLE的时候为1
                                ActionCodeLst.Add(result);
                            }
                            else
                            {
                                //位置的调整，后面的随从的位置需要调整
                                ActionCodeLst.Add(resultArray[0] + CardUtility.strSplitMark + resultArray[1] + CardUtility.strSplitMark +
                                (int.Parse(resultArray[2]) + 1).ToString() + CardUtility.strSplitMark + resultArray[3]);
                            }
                        }
                        PlayInfo.BattleField.PutToBattle(MinionPos, minion);
                        break;
                    case MinionCard.战吼类型枚举.相邻:
                    case MinionCard.战吼类型枚举.自身:
                        PlayInfo.BattleField.PutToBattle(MinionPos, minion);
                        PlayInfo.BattleField.发动战吼(MinionPos, game);
                        break;
                    default:
                        break;
                }
                PlayInfo.BattleField.ResetBuff();
            }
            else
            {
                ActionCodeLst.Clear();
            }
        }

        private static void UseSpell(ActionStatus game, String CardSn, Engine.Card.CardBasicInfo card, List<String> ActionCodeLst, PublicInfo PlayInfo)
        {
            ActionCodeLst.Add(ActionCode.strAbility + CardUtility.strSplitMark + CardSn);
            //初始化 Buff效果等等
            Engine.Card.SpellCard ablity = (Engine.Card.SpellCard)CardUtility.GetCardInfoBySN(CardSn);
            var ResultArg = ablity.UseSpell(game);
            if (ResultArg.Count != 0)
            {
                ActionCodeLst.AddRange(ResultArg);
                //英雄技能等的时候，不算[本方施法] 
                if (card.原生卡牌)
                    game.eventhandler.事件池.Add(new Engine.Utility.CardUtility.全局事件()
                    {
                        触发事件类型 = CardUtility.事件类型枚举.施法,
                        触发位置 = PlayInfo.战场位置
                    });
            }
            else
            {
                ActionCodeLst.Clear();
            }
        }

        /// <summary>
        /// 攻击动作
        /// </summary>
        /// <param name="game"></param>
        /// <param name="MyPos">攻击方</param>
        /// <param name="YourPos">被攻击方</param>
        /// <param name="IsMyAction">动作发起方</param>
        /// <returns></returns>
        public static List<String> Fight(ActionStatus game, int MyPos, int YourPos, Boolean IsMyAction)
        {
            game.eventhandler.事件池.Clear();
            //FIGHT#1#2
            String actionCode = ActionCode.strFight + CardUtility.strSplitMark + MyPos + CardUtility.strSplitMark + YourPos;
            List<String> ActionCodeLst = new List<string>();
            ActionCodeLst.Add(actionCode);
            ActionCodeLst.AddRange(FightHandler.Fight(MyPos, YourPos, game, IsMyAction));
            ActionCodeLst.AddRange(game.eventhandler.事件处理(game));
            return ActionCodeLst;
        }
    }
}
