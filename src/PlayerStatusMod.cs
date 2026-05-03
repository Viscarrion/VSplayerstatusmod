using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PlayerStatusMod
{
    [ProtoContract]
    public class StatusUpdatePacket
    {
        [ProtoMember(1)]
        public int StatusMask;
    }

    [ProtoContract]
    public class StatusBroadcastPacket
    {
        [ProtoMember(1)]
        public List<string> PlayerNames = new List<string>();
        [ProtoMember(2)]
        public List<int> StatusMasks = new List<int>();
    }

    public static class PlayerStatus
    {
        public const int None      = 0;
        public const int Chilling  = 1 << 0;
        public const int Recording = 1 << 1;
        public const int Streaming = 1 << 2;
        public const int Ignore    = 1 << 3;
        public const int RP        = 1 << 4;

        public static readonly int[] Bits = { Chilling, Recording, Streaming, Ignore, RP };

        // Colored block character per status — shown next to player names
        public static readonly string[] VtmlBlocks =
        {
            "<font color=\"#38cc38\">■</font>",
            "<font color=\"#e63333\">■</font>",
            "<font color=\"#a633e6\">■</font>",
            "<font color=\"#8c8c8c\">■</font>",
            "<font color=\"#f0d91a\">■</font>",
        };

        // Plain labels for the toggle buttons
        public static readonly string[] ButtonLabels =
        {
            "Chilling", "Recording", "Streaming", "Ignore Me", "Open to RP",
        };

        public static int Count => Bits.Length;
        public static bool Has(int mask, int bit) => (mask & bit) != 0;

        public static string MaskToVtml(int mask)
        {
            if (mask == None) return "";
            var parts = new List<string>();
            for (int i = 0; i < Count; i++)
                if (Has(mask, Bits[i]))
                    parts.Add(VtmlBlocks[i]);
            // Blocks separated by a thin space
            return " " + string.Join(" ", parts);
        }
    }

    public class GuiDialogPlayerStatus : GuiDialog
    {
        public override string ToggleKeyCombinationCode => "playerstatusgui";

        private List<string> playerNames = new List<string>();
        private List<int>    playerMasks  = new List<int>();
        private int myMask = PlayerStatus.None;

        public Action<int> OnStatusChanged;

        private const double DW   = 300;
        private const double Pad  = 10;
        private const double RowH = 22;
        private const double BtnW = 82;
        private const double BtnH = 24;
        private const double BtnG = 4;

        public GuiDialogPlayerStatus(ICoreClientAPI capi) : base(capi) { }

        public void SetData(List<string> names, List<int> masks, int myMask)
        {
            playerNames  = names;
            playerMasks  = masks;
            this.myMask  = myMask;
            if (IsOpened()) Rebuild();
        }

        public override void OnGuiOpened() => Rebuild();

        private void Rebuild()
        {
            int n = playerNames.Count;

            // Build player list VTML — name + colored block squares
            var sb = new System.Text.StringBuilder();
            if (n == 0)
            {
                sb.Append("<font color=\"#888888\">No players online</font>");
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) sb.Append("\n");
                    int mask = (i < playerMasks.Count) ? playerMasks[i] : PlayerStatus.None;
                    sb.Append(playerNames[i]);
                    string blocks = PlayerStatus.MaskToVtml(mask);
                    if (blocks.Length > 0) sb.Append(blocks);
                }
            }
            string richText = sb.ToString();

            // Heights
            double listH   = Math.Max(n, 1) * RowH + 4;
            double labelH  = 18;
            double row1Y   = Pad + listH + 8;
            double row2Y   = row1Y + labelH + BtnG + BtnH + BtnG;
            double bgH     = row2Y + BtnH + Pad;
            double totalH  = 30 + bgH;  // 30px title bar

            ElementBounds dlgBounds = ElementBounds.Fixed(
                EnumDialogArea.RightMiddle, -20, 0, DW, totalH);

            // Background starts AT y=0 relative to dialog (the title bar draws on top of it)
            // This ensures the shaded bg sits behind the player name list
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, DW, totalH);
            bgBounds.BothSizing = ElementSizing.Fixed;

            // Content positioned below title bar
            double contentY = 30 + Pad; // below title
            ElementBounds listBounds  = ElementBounds.Fixed(Pad, contentY, DW - Pad * 2, listH);
            ElementBounds labelBounds = ElementBounds.Fixed(Pad, contentY + listH + 8, DW - Pad * 2, labelH);

            double btn1Y = contentY + listH + 8 + labelH + BtnG;
            double btn2Y = btn1Y + BtnH + BtnG;

            ElementBounds b0 = ElementBounds.Fixed(Pad + 0 * (BtnW + BtnG), btn1Y, BtnW, BtnH);
            ElementBounds b1 = ElementBounds.Fixed(Pad + 1 * (BtnW + BtnG), btn1Y, BtnW, BtnH);
            ElementBounds b2 = ElementBounds.Fixed(Pad + 2 * (BtnW + BtnG), btn1Y, BtnW, BtnH);
            ElementBounds b3 = ElementBounds.Fixed(Pad + 0 * (BtnW + BtnG), btn2Y, BtnW, BtnH);
            ElementBounds b4 = ElementBounds.Fixed(Pad + 1 * (BtnW + BtnG), btn2Y, BtnW, BtnH);
            ElementBounds bc = ElementBounds.Fixed(Pad + 2 * (BtnW + BtnG), btn2Y, BtnW, BtnH);

            var font = CairoFont.ButtonText().WithFontSize(11);

            SingleComposer = capi.Gui.CreateCompo("playerstatusdialog", dlgBounds)
                .AddShadedDialogBG(bgBounds, withTitleBar: true)
                .AddDialogTitleBar("Online Players (" + n + ")", () => TryClose())
                .AddRichtext(richText, CairoFont.WhiteDetailText(), listBounds, "playerlist")
                .AddStaticText("Your status:", CairoFont.WhiteSmallText(), labelBounds)
                .AddToggleButton(PlayerStatus.ButtonLabels[0], font, on => Toggle(0, on), b0, "sb0")
                .AddToggleButton(PlayerStatus.ButtonLabels[1], font, on => Toggle(1, on), b1, "sb1")
                .AddToggleButton(PlayerStatus.ButtonLabels[2], font, on => Toggle(2, on), b2, "sb2")
                .AddToggleButton(PlayerStatus.ButtonLabels[3], font, on => Toggle(3, on), b3, "sb3")
                .AddToggleButton(PlayerStatus.ButtonLabels[4], font, on => Toggle(4, on), b4, "sb4")
                .AddSmallButton("Clear All", OnClear, bc, EnumButtonStyle.Small, "clearbtn")
                .Compose();

            SyncToggleStates();
        }

        private void Toggle(int index, bool on)
        {
            if (on) myMask |=  PlayerStatus.Bits[index];
            else    myMask &= ~PlayerStatus.Bits[index];
            OnStatusChanged?.Invoke(myMask);
            Rebuild();
        }

        private bool OnClear()
        {
            myMask = PlayerStatus.None;
            OnStatusChanged?.Invoke(myMask);
            Rebuild();
            return true;
        }

        private void SyncToggleStates()
        {
            if (SingleComposer == null) return;
            for (int i = 0; i < PlayerStatus.Count; i++)
                SingleComposer.GetToggleButton("sb" + i)
                    ?.SetValue(PlayerStatus.Has(myMask, PlayerStatus.Bits[i]));
        }

        public override bool PrefersUngrabbedMouse => false;
    }

    public class PlayerStatusSystem : ModSystem
    {
        private const string Channel = "playerstatus";

        public override void Start(ICoreAPI api)
        {
            api.Network
                .RegisterChannel(Channel)
                .RegisterMessageType(typeof(StatusUpdatePacket))
                .RegisterMessageType(typeof(StatusBroadcastPacket));
        }

        // ── Server ────────────────────────────────────────────────────

        private IServerNetworkChannel serverChannel;
        private ICoreServerAPI sapi;
        private readonly Dictionary<string, int> maskMap = new Dictionary<string, int>();

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            serverChannel = api.Network.GetChannel(Channel)
                .SetMessageHandler<StatusUpdatePacket>(OnClientUpdate);
            api.Event.PlayerNowPlaying += OnJoin;
            api.Event.PlayerDisconnect  += OnLeave;
        }

        private void OnJoin(IServerPlayer p)
        {
            serverChannel.SendPacket(BuildBroadcast(), p);
            BroadcastAll();
        }

        private void OnLeave(IServerPlayer p)
        {
            maskMap.Remove(p.PlayerUID);
            BroadcastAll();
        }

        private void OnClientUpdate(IServerPlayer from, StatusUpdatePacket pkt)
        {
            if (pkt.StatusMask == PlayerStatus.None)
                maskMap.Remove(from.PlayerUID);
            else
                maskMap[from.PlayerUID] = pkt.StatusMask;
            BroadcastAll();
        }

        private void BroadcastAll() => serverChannel.BroadcastPacket(BuildBroadcast());

        private StatusBroadcastPacket BuildBroadcast()
        {
            var pkt = new StatusBroadcastPacket();
            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                pkt.PlayerNames.Add(p.PlayerName);
                pkt.StatusMasks.Add(
                    maskMap.TryGetValue(p.PlayerUID, out int m) ? m : PlayerStatus.None);
            }
            return pkt;
        }

        // ── Client ────────────────────────────────────────────────────

        private IClientNetworkChannel clientChannel;
        private ICoreClientAPI capi;
        private GuiDialogPlayerStatus dialog;

        private List<string> cachedNames = new List<string>();
        private List<int>    cachedMasks  = new List<int>();
        private int myCurrentMask = PlayerStatus.None;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.GetChannel(Channel)
                .SetMessageHandler<StatusBroadcastPacket>(OnBroadcast);

            dialog = new GuiDialogPlayerStatus(api);
            dialog.OnStatusChanged = OnMyStatusChanged;

            api.Input.RegisterHotKey(
                "playerstatusgui", "Show Player List",
                GlKeys.Comma, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("playerstatusgui", _ => ToggleDialog());

            api.Event.PlayerJoin  += p => EnsureInCache(p.PlayerName, PlayerStatus.None);
            api.Event.PlayerLeave += p =>
            {
                int idx = cachedNames.IndexOf(p.PlayerName);
                if (idx >= 0) { cachedNames.RemoveAt(idx); cachedMasks.RemoveAt(idx); }
                if (dialog.IsOpened())
                    dialog.SetData(cachedNames, cachedMasks, myCurrentMask);
            };
        }

        private void EnsureInCache(string name, int mask)
        {
            if (!cachedNames.Contains(name))
            {
                cachedNames.Add(name);
                cachedMasks.Add(mask);
            }
        }

        private bool ToggleDialog()
        {
            if (dialog.IsOpened())
            {
                dialog.TryClose();
            }
            else
            {
                string me = capi.World.Player?.PlayerName;
                if (me != null) EnsureInCache(me, myCurrentMask);
                dialog.SetData(cachedNames, cachedMasks, myCurrentMask);
                dialog.TryOpen();
            }
            return true;
        }

        private void OnBroadcast(StatusBroadcastPacket pkt)
        {
            cachedNames = pkt.PlayerNames;
            cachedMasks  = pkt.StatusMasks;

            string me = capi.World.Player?.PlayerName;
            if (me != null)
            {
                int idx = cachedNames.IndexOf(me);
                if (idx >= 0) myCurrentMask = cachedMasks[idx];
            }

            if (dialog.IsOpened())
                dialog.SetData(cachedNames, cachedMasks, myCurrentMask);
        }

        private void OnMyStatusChanged(int newMask)
        {
            myCurrentMask = newMask;
            string me = capi.World.Player?.PlayerName;
            if (me != null)
            {
                int idx = cachedNames.IndexOf(me);
                if (idx >= 0) cachedMasks[idx] = newMask;
            }
            clientChannel.SendPacket(new StatusUpdatePacket { StatusMask = newMask });
        }
    }
}
