using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.IO;
using TerrariaApi.Server;
using TShockAPI;

namespace PlayerExport
{
    [ApiVersion(2, 1)]
    public class PlayerExport : TerrariaPlugin
    {
        public override string Name => "PlayerExport";
        public override Version Version => new Version(1, 0, 1);
        public override string Author => "Megghy";
        public override string Description => "导出服务器中的云存档.";

        public PlayerExport(Main game) : base(game)
        {
        }
        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("playerexport", async delegate (CommandArgs args)
            {
                await Task.Run(() =>
                {
                    var cmd = args.Parameters;
                    if (cmd.Count >= 1)
                    {
                        switch (cmd[0])
                        {
                            case "all":
                                int successcount = 0;
                                int faildcount = 0;
                                var savedlist = new List<string>();
                                TShock.Players.Where(p => p != null && p.SaveServerCharacter()).ForEach(plr =>
                                {
                                    savedlist.Add(plr.Name);
                                    if (Export(plr.TPlayer).Result) { args.Player.SendSuccessMessage($"已导出 {plr.Name} 的在线存档."); successcount++; }
                                    else { args.Player.SendErrorMessage($"导出 {plr.Name} 的在线存档时发生错误."); faildcount++; }
                                });
                                var allaccount = TShock.UserAccounts.GetUserAccounts();
                                allaccount.Where(acc => acc != null && !savedlist.Contains(acc.Name)).ForEach(acc =>
                                {
                                    var data = TShock.CharacterDB.GetPlayerData(new TSPlayer(-1), acc.ID);
                                    if (data != null)
                                    {
                                        if (data.hideVisuals != null)
                                        {
                                            if (Export(ModifyData(acc.Name, data)).Result) { args.Player.SendSuccessMessage($"已导出 {acc.Name} 的存档."); successcount++; }
                                            else { args.Player.SendErrorMessage($"导出 {acc.Name} 的存档时发生错误."); faildcount++; }
                                        }
                                        else args.Player.SendInfoMessage($"玩家 {acc.Name} 的数据不完整, 已跳过.");
                                    }
                                });
                                args.Player.SendInfoMessage($"操作完成. 成功: {successcount}, 失败: {faildcount}.");
                                break;
                            case "single":
                                if (cmd.Count > 1)
                                {
                                    var name = cmd[1];
                                    var list = TSPlayer.FindByNameOrID(name);
                                    if (list.Count > 1) args.Player.SendMultipleMatchError(list);
                                    else if (list.Any())
                                    {
                                        if (Export(list[0].TPlayer).Result) args.Player.SendSuccessMessage($"已导出玩家 {list[0].Name} 的存档至 {Environment.CurrentDirectory + "\\PlayerExport\\" + list[0].Name + ".plr"}.");
                                        else args.Player.SendErrorMessage($"导出失败.");
                                    }
                                    else
                                    {
                                        var offlinelist = TShock.UserAccounts.GetUserAccountsByName(name);
                                        if (offlinelist.Count > 1) args.Player.SendMultipleMatchError(offlinelist);
                                        else if (offlinelist.Any())
                                        {
                                            name = offlinelist[0].Name;
                                            args.Player.SendInfoMessage($"玩家 {name} 未在线, 将导出离线存档...");
                                            var data = TShock.CharacterDB.GetPlayerData(new TSPlayer(-1), offlinelist[0].ID);
                                            if (data != null)
                                            {
                                                if (data.hideVisuals == null)
                                                {
                                                    args.Player.SendErrorMessage($"玩家 {name} 的数据不完整, 无法导出.");
                                                    return;
                                                }
                                                if (Export(ModifyData(name, data)).Result) args.Player.SendSuccessMessage($"已导出玩家 {name} 的存档至 {Environment.CurrentDirectory + "\\PlayerExport\\" + name + ".plr"}.");
                                                else args.Player.SendErrorMessage($"导出失败.");
                                            }
                                            else
                                            {
                                                args.Player.SendErrorMessage($"未能从数据库中获取到玩家数据.");
                                            }
                                        }
                                        else
                                        {
                                            args.Player.SendErrorMessage($"未找到名称中包含 {name} 的玩家.");
                                        }
                                    }
                                }
                                else
                                {
                                    args.Player.SendErrorMessage("格式错误. /export single 玩家名");
                                }
                                break;
                            default:
                                args.Player.SendErrorMessage("无效的命令. 1. /export single 玩家名 <导出单个玩家的存档>, 2. /export all <导出所有玩家的存档>");
                                break;
                        }
                    }
                });
            }, new string[] { "export", "导出" })
            { HelpText = "使TShock云存档可以导出为本地存档." });
        }
        public static Player ModifyData(string name, PlayerData data)
        {
            var plr = new Player();
            if (data != null)
            {
                plr.name = name;
                plr.statLife = data.health;
                plr.statLifeMax = data.maxHealth;
                plr.statMana = data.mana;
                plr.statManaMax = data.maxMana;
                plr.SpawnX = data.spawnX;
                plr.SpawnY = data.spawnY;
                plr.skinVariant = data.skinVariant ?? default;
                plr.hair = data.hair ?? default;
                plr.hairDye = data.hairDye;
                plr.hairColor = data.hairColor ?? default;
                plr.pantsColor = data.pantsColor ?? default;
                plr.underShirtColor = data.underShirtColor ?? default;
                plr.shoeColor = data.shoeColor ?? default;
                plr.hideVisibleAccessory = data.hideVisuals;
                plr.skinColor = data.skinColor ?? default;
                plr.eyeColor = data.eyeColor ?? default;
                for (int i = 0; i < 260; i++)
                {
                    if (i < 59) plr.inventory[i] = NetItem2Item(data.inventory[i]);
                    else if (i >= 59 && i < 79) plr.armor[i - 59] = NetItem2Item(data.inventory[i]);
                    else if (i >= 79 && i < 89) plr.dye[i - 79] = NetItem2Item(data.inventory[i]);
                    else if (i >= 89 && i < 94) plr.miscEquips[i - 89] = NetItem2Item(data.inventory[i]);
                    else if (i >= 94 && i < 99) plr.miscDyes[i - 94] = NetItem2Item(data.inventory[i]);
                    else if (i >= 99 && i < 139) plr.bank.item[i - 99] = NetItem2Item(data.inventory[i]);
                    else if (i >= 139 && i < 179) plr.bank2.item[i - 139] = NetItem2Item(data.inventory[i]);
                    else if (i == 179) plr.trashItem = NetItem2Item(data.inventory[i]);
                    else if (i >= 180 && i < 220) plr.bank3.item[i - 180] = NetItem2Item(data.inventory[i]);
                    else if (i >= 220 && i < 260) plr.bank4.item[i - 220] = NetItem2Item(data.inventory[i]);
                }
            }
            return plr;
        }
        public static Item NetItem2Item(NetItem item)
        {
            var i = new Item();
            i.SetDefaults(item.NetId);
            i.stack = item.Stack;
            i.prefix = item.PrefixId;
            return i;
        }
        public static async Task<bool> Export(Player plr)
        {
            return await Task.Run(() =>
            {
                string path = Environment.CurrentDirectory + "\\PlayerExport\\" + plr.name + ".plr";
                try
                {
                    if (!Directory.Exists(Environment.CurrentDirectory + "\\PlayerExport")) Directory.CreateDirectory(Environment.CurrentDirectory + "\\PlayerExport");
                    if (File.Exists(path))
                    {
                        File.Copy(path, path + ".bak", true);
                    }
                    RijndaelManaged rijndaelManaged = new RijndaelManaged();
                    using (Stream stream = new FileStream(path, FileMode.Create))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(stream, rijndaelManaged.CreateEncryptor(Player.ENCRYPTION_KEY, Player.ENCRYPTION_KEY), CryptoStreamMode.Write))
                        {
                            PlayerFileData playerFileData = new PlayerFileData
                            {
                                Metadata = FileMetadata.FromCurrentSettings(FileType.Player),
                                Player = plr,
                                _isCloudSave = false,
                                _path = path
                            };
                            Main.LocalFavoriteData.ClearEntry(playerFileData);
                            using (BinaryWriter binaryWriter = new BinaryWriter(cryptoStream))
                            {
                                binaryWriter.Write(230);
                                playerFileData.Metadata.Write(binaryWriter);
                                binaryWriter.Write(plr.name);
                                binaryWriter.Write(plr.difficulty);
                                binaryWriter.Write(playerFileData.GetPlayTime().Ticks);
                                binaryWriter.Write(plr.hair);
                                binaryWriter.Write(plr.hairDye);
                                BitsByte bb = 0;
                                for (int i = 0; i < 8; i++)
                                {
                                    bb[i] = plr.hideVisibleAccessory[i];
                                }
                                binaryWriter.Write(bb);
                                bb = 0;
                                for (int j = 0; j < 2; j++)
                                {
                                    bb[j] = plr.hideVisibleAccessory[j + 8];
                                }
                                binaryWriter.Write(bb);
                                binaryWriter.Write(plr.hideMisc);
                                binaryWriter.Write((byte)plr.skinVariant);
                                binaryWriter.Write(plr.statLife);
                                binaryWriter.Write(plr.statLifeMax);
                                binaryWriter.Write(plr.statMana);
                                binaryWriter.Write(plr.statManaMax);
                                binaryWriter.Write(plr.extraAccessory);
                                binaryWriter.Write(plr.unlockedBiomeTorches);
                                binaryWriter.Write(plr.UsingBiomeTorches);
                                binaryWriter.Write(plr.downedDD2EventAnyDifficulty);
                                binaryWriter.Write(plr.taxMoney);
                                binaryWriter.Write(plr.hairColor.R);
                                binaryWriter.Write(plr.hairColor.G);
                                binaryWriter.Write(plr.hairColor.B);
                                binaryWriter.Write(plr.skinColor.R);
                                binaryWriter.Write(plr.skinColor.G);
                                binaryWriter.Write(plr.skinColor.B);
                                binaryWriter.Write(plr.eyeColor.R);
                                binaryWriter.Write(plr.eyeColor.G);
                                binaryWriter.Write(plr.eyeColor.B);
                                binaryWriter.Write(plr.shirtColor.R);
                                binaryWriter.Write(plr.shirtColor.G);
                                binaryWriter.Write(plr.shirtColor.B);
                                binaryWriter.Write(plr.underShirtColor.R);
                                binaryWriter.Write(plr.underShirtColor.G);
                                binaryWriter.Write(plr.underShirtColor.B);
                                binaryWriter.Write(plr.pantsColor.R);
                                binaryWriter.Write(plr.pantsColor.G);
                                binaryWriter.Write(plr.pantsColor.B);
                                binaryWriter.Write(plr.shoeColor.R);
                                binaryWriter.Write(plr.shoeColor.G);
                                binaryWriter.Write(plr.shoeColor.B);
                                for (int k = 0; k < plr.armor.Length; k++)
                                {
                                    binaryWriter.Write(plr.armor[k].netID);
                                    binaryWriter.Write(plr.armor[k].prefix);
                                }
                                for (int l = 0; l < plr.dye.Length; l++)
                                {
                                    binaryWriter.Write(plr.dye[l].netID);
                                    binaryWriter.Write(plr.dye[l].prefix);
                                }
                                for (int m = 0; m < 58; m++)
                                {
                                    binaryWriter.Write(plr.inventory[m].netID);
                                    binaryWriter.Write(plr.inventory[m].stack);
                                    binaryWriter.Write(plr.inventory[m].prefix);
                                    binaryWriter.Write(plr.inventory[m].favorited);
                                }
                                for (int n = 0; n < plr.miscEquips.Length; n++)
                                {
                                    binaryWriter.Write(plr.miscEquips[n].netID);
                                    binaryWriter.Write(plr.miscEquips[n].prefix);
                                    binaryWriter.Write(plr.miscDyes[n].netID);
                                    binaryWriter.Write(plr.miscDyes[n].prefix);
                                }
                                for (int num = 0; num < 40; num++)
                                {
                                    binaryWriter.Write(plr.bank.item[num].netID);
                                    binaryWriter.Write(plr.bank.item[num].stack);
                                    binaryWriter.Write(plr.bank.item[num].prefix);
                                }
                                for (int num2 = 0; num2 < 40; num2++)
                                {
                                    binaryWriter.Write(plr.bank2.item[num2].netID);
                                    binaryWriter.Write(plr.bank2.item[num2].stack);
                                    binaryWriter.Write(plr.bank2.item[num2].prefix);
                                }
                                for (int num3 = 0; num3 < 40; num3++)
                                {
                                    binaryWriter.Write(plr.bank3.item[num3].netID);
                                    binaryWriter.Write(plr.bank3.item[num3].stack);
                                    binaryWriter.Write(plr.bank3.item[num3].prefix);
                                }
                                for (int num4 = 0; num4 < 40; num4++)
                                {
                                    binaryWriter.Write(plr.bank4.item[num4].netID);
                                    binaryWriter.Write(plr.bank4.item[num4].stack);
                                    binaryWriter.Write(plr.bank4.item[num4].prefix);
                                }
                                binaryWriter.Write(plr.voidVaultInfo);
                                for (int num5 = 0; num5 < 22; num5++)
                                {
                                    if (Main.buffNoSave[plr.buffType[num5]])
                                    {
                                        binaryWriter.Write(0);
                                        binaryWriter.Write(0);
                                    }
                                    else
                                    {
                                        binaryWriter.Write(plr.buffType[num5]);
                                        binaryWriter.Write(plr.buffTime[num5]);
                                    }
                                }
                                for (int num6 = 0; num6 < 200; num6++)
                                {
                                    if (plr.spN[num6] == null)
                                    {
                                        binaryWriter.Write(-1);
                                        break;
                                    }
                                    binaryWriter.Write(plr.spX[num6]);
                                    binaryWriter.Write(plr.spY[num6]);
                                    binaryWriter.Write(plr.spI[num6]);
                                    binaryWriter.Write(plr.spN[num6]);
                                }
                                binaryWriter.Write(plr.hbLocked);
                                for (int num7 = 0; num7 < plr.hideInfo.Length; num7++)
                                {
                                    binaryWriter.Write(plr.hideInfo[num7]);
                                }
                                binaryWriter.Write(plr.anglerQuestsFinished);
                                for (int num8 = 0; num8 < plr.DpadRadial.Bindings.Length; num8++)
                                {
                                    binaryWriter.Write(plr.DpadRadial.Bindings[num8]);
                                }
                                for (int num9 = 0; num9 < plr.builderAccStatus.Length; num9++)
                                {
                                    binaryWriter.Write(plr.builderAccStatus[num9]);
                                }
                                binaryWriter.Write(plr.bartenderQuestLog);
                                binaryWriter.Write(plr.dead);
                                if (plr.dead)
                                {
                                    binaryWriter.Write(plr.respawnTimer);
                                }
                                long value = DateTime.UtcNow.ToBinary();
                                binaryWriter.Write(value);
                                binaryWriter.Write(plr.golferScoreAccumulated);
                                plr.creativeTracker.Save(binaryWriter);
                                plr.SaveTemporaryItemSlotContents(binaryWriter);
                                CreativePowerManager.Instance.SaveToPlayer(plr, binaryWriter);
                                binaryWriter.Flush();
                                cryptoStream.FlushFinalBlock();
                                stream.Flush();

                            }
                        }
                    }
                    return true;
                }
                catch (Exception ex) { File.Delete(path); TShock.Log.ConsoleError(ex.Message); }
                return false;
            });
        }
    }
}
