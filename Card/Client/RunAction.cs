﻿using Card.Server;
using System;
using System.Collections.Generic;

namespace Card.Client
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
        #region"开始动作"
        /// <summary>
        /// 开始一个动作
        /// </summary>
        /// <param name="game"></param>
        /// <param name="CardSn"></param>
        /// <param name="ConvertPosDirect">亡语的时候，需要倒置方向</param>
        /// <returns></returns>
        public static List<String> StartAction(GameManager game, String CardSn, Boolean ConvertPosDirect = false)
        {
            //清除事件池，注意，事件将在动作结束后整体结算
            game.事件池.Clear();
            Card.CardBasicInfo card = Card.CardUtility.GetCardInfoBySN(CardSn);
            List<String> ActionCodeLst = new List<string>();
            switch (card.CardType)
            {
                case CardBasicInfo.CardTypeEnum.法术:
                    ActionCodeLst.Add(ActionCode.strAbility + CardUtility.strSplitMark + CardSn);
                    //初始化 Buff效果等等
                    Card.AbilityCard ablity = (Card.AbilityCard)CardUtility.GetCardInfoBySN(CardSn);
                    //连击效果的法术修改
                    if (game.MyInfo.连击状态 && (!String.IsNullOrEmpty(card.连击效果)))
                    {
                        ablity = (Card.AbilityCard)CardUtility.GetCardInfoBySN(card.连击效果);
                    }
                    ablity.CardAbility.Init();
                    var ResultArg = game.UseAbility(ablity, ConvertPosDirect);
                    if (ResultArg.Count != 0)
                    {
                        ActionCodeLst.AddRange(ResultArg);
                        //英雄技能等的时候，不算[本方施法] 
                        if (CardSn.Substring(1, 1) == Card.AbilityCard.原生法术)
                            game.事件池.Add(new Card.CardUtility.全局事件()
                            {
                                事件类型 = CardUtility.事件类型列表.施法,
                                触发方向 = CardUtility.TargetSelectDirectEnum.本方,
                                触发位置 = Card.Client.BattleFieldInfo.HeroPos
                            });
                    }
                    else
                    {
                        ActionCodeLst.Clear();
                    }
                    break;
                case CardBasicInfo.CardTypeEnum.随从:
                    int MinionPos = 1;
                    if (game.MyInfo.BattleField.MinionCount != 0) MinionPos = GetPutPos(game);
                    if (MinionPos != -1)
                    {
                        ActionCodeLst.Add(ActionCode.strMinion + CardUtility.strSplitMark + CardSn + CardUtility.strSplitMark + MinionPos.ToString("D1"));
                        var minion = (Card.MinionCard)card;
                        //初始化
                        minion.Init();
                        //必须在放入之前做得原因是，被放入的随从不能被触发这个事件
                        game.事件池.Add(new Card.CardUtility.全局事件()
                        {
                            事件类型 = CardUtility.事件类型列表.召唤,
                            附加信息 = minion.种族.ToString(),
                            触发位置 = MinionPos
                        });
                        switch (minion.战吼类型)
                        {
                            case MinionCard.战吼类型列表.默认:
                                game.MyInfo.BattleField.PutToBattle(MinionPos, minion);
                                ActionCodeLst.AddRange(minion.发动战吼(game));
                                break;
                            case MinionCard.战吼类型列表.抢先:
                                //战吼中，其他 系列的法术效果
                                foreach (var result in minion.发动战吼(game))
                                {
                                    var resultArray = result.Split(CardUtility.strSplitMark.ToCharArray());
                                    if (int.Parse(resultArray[2]) < MinionPos)
                                    {
                                        ActionCodeLst.Add(result);
                                    }
                                    else
                                    {
                                        ActionCodeLst.Add(resultArray[0] + CardUtility.strSplitMark + resultArray[1] + CardUtility.strSplitMark +
                                                           (int.Parse(resultArray[2]) + 1).ToString() + CardUtility.strSplitMark + resultArray[3]);
                                    }
                                }
                                game.MyInfo.BattleField.PutToBattle(MinionPos, minion);
                                break;
                            case MinionCard.战吼类型列表.相邻:
                            case MinionCard.战吼类型列表.自身:
                                game.MyInfo.BattleField.PutToBattle(MinionPos, minion);
                                game.MyInfo.BattleField.发动战吼(MinionPos);
                                break;
                            default:
                                break;
                        }
                        game.MyInfo.BattleField.ResetBuff();
                    }
                    else
                    {
                        ActionCodeLst.Clear();
                    }
                    break;
                case CardBasicInfo.CardTypeEnum.武器:
                    ActionCodeLst.Add(ActionCode.strWeapon + CardUtility.strSplitMark + CardSn);
                    game.MyInfo.Weapon = (Card.WeaponCard)card;
                    break;
                case CardBasicInfo.CardTypeEnum.奥秘:
                    ActionCodeLst.Add(ActionCode.strSecret + CardUtility.strSplitMark + CardSn);
                    game.MySelfInfo.奥秘列表.Add((Card.SecretCard)card);
                    game.MyInfo.SecretCount = game.MySelfInfo.奥秘列表.Count;
                    break;
                default:
                    break;
            }
            //随从卡牌的连击效果启动
            if (card.CardType != CardBasicInfo.CardTypeEnum.法术 && game.MyInfo.连击状态)
            {
                if (!String.IsNullOrEmpty(card.连击效果))
                {
                    //初始化 Buff效果等等
                    Card.AbilityCard ablity = (Card.AbilityCard)CardUtility.GetCardInfoBySN(card.连击效果);
                    ablity.CardAbility.Init();
                    var ResultArg = game.UseAbility(ablity, ConvertPosDirect);
                    if (ResultArg.Count != 0)
                    {
                        ActionCodeLst.AddRange(ResultArg);
                        //英雄技能等的时候，不算[本方施法] 
                        if (CardSn.Substring(1, 1) == Card.AbilityCard.原生法术)
                            game.事件池.Add(new Card.CardUtility.全局事件()
                            {
                                事件类型 = CardUtility.事件类型列表.施法,
                                触发方向 = CardUtility.TargetSelectDirectEnum.本方,
                                触发位置 = Card.Client.BattleFieldInfo.HeroPos
                            });
                    }
                }
            }
            if (ActionCodeLst.Count != 0)
            {
                game.MyInfo.连击状态 = true;
                ActionCodeLst.AddRange(game.事件处理());
            }
            return ActionCodeLst;
        }

        /// <summary>
        /// 战斗
        /// </summary>
        /// <param name="game"></param>
        /// <param name="MyPos"></param>
        /// <param name="YourPos"></param>
        /// <returns></returns>
        public static List<String> Fight(GameManager game, int MyPos, int YourPos)
        {
            game.事件池.Clear();
            //FIGHT#1#2
            String actionCode = ActionCode.strFight + CardUtility.strSplitMark + MyPos + CardUtility.strSplitMark + YourPos;
            List<String> ActionCodeLst = new List<string>();
            ActionCodeLst.Add(actionCode);
            ActionCodeLst.AddRange(game.Fight(MyPos, YourPos, false));
            ActionCodeLst.AddRange(game.事件处理());
            return ActionCodeLst;
        }
        #endregion
    }
}
