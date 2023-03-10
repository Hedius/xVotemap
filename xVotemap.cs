/*

Original authors: onegrizzlybeer/grizzlybeer, Hexacanon EG modification, Hand of Shadow, <= 1.3.1 by aether

Maintainer: Hedius
License for all changes made by H3dius: Version: >= 1.5.7.0

Copyright (C) 2023. Hedius gitlab.com/hedius

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License Version 3
along with this program.  If not, see https://www.gnu.org/licenses/gpl-3.0.en.html>.
 */

using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
//using System.Drawing;


using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;
using System.Net;

namespace PRoConEvents
{
    public class xVotemap : PRoConPluginAPI, IPRoConPluginInterface
    {
        #region Variables and Constructors

        private object derplock = new object();

        private string m_strHostName;
        private string m_strPort;
        private string m_strPRoConVersion;

        private DateTime lastcheck = DateTime.Now.AddMinutes(-3.0);
        private DateTime lastupdatecheck = DateTime.Now.AddHours(-4);

        // User Settings
        private int m_iVoteThres = 1;
        private int m_iRandomness = 5;
        private int m_iNumOfMapOptions = 4;
        private int m_iVotingDuration = 300;
        private int m_iStopVoteTime = 180;
        private int m_iRushVoteStartTime = 600;  // in seconds
        private int m_iCalVoteStartTime = 600;  // in seconds
        private int minimumPlayers = 0;
        private int maximumPlayers = 70;
        private enumBoolYesNo m_enumAllowSpectatorsToVote = enumBoolYesNo.No;

        private string m_strHosVotePrefix = @"/";
        private string m_strTrigger = "Automatic";
        private string m_strEnableVoteBanner = "First";
        private string m_strBannerType = "Both";
        private int m_iBannerYellDuration = 5;
        private enumBoolYesNo m_enumExcludeCurrMap = enumBoolYesNo.Yes;
        private enumBoolYesNo m_enumExcludeCurrMode = enumBoolYesNo.No;
        private int m_iVotingOptionsInterval = 60;
        private int m_iNextMapDisplayInterval = 600;
        private enumBoolYesNo m_enumShowGamemode = enumBoolYesNo.Yes;

        private int m_iDebugLevel = 3;

        // The Rest
        private List<TeamScore> m_listCurrTeamScore = new List<TeamScore>();
        //private List<CPlayerInfo> m_listCurrPlayers = new List<CPlayerInfo>();
        private List<MaplistEntry> m_listCurrMapList = new List<MaplistEntry>();
        private Dictionary<string, int> m_dictVoting = new Dictionary<string, int>();     // Player Name, Vote
        private List<string> m_listMapOptions;            // Map name
        private List<string> m_listGamemodeOptions;       // gamemode
        private List<MaplistEntry> m_listPastMaps = new List<MaplistEntry>();
        private int m_iCurrPlayerCount = 0;
        private DateTime m_timeServerInfoCheck = DateTime.Now;

        private string mapSortSettings = "Map and Gametype";
        private string m_strCurrentGameMode = "";
        private string m_strCurrentMap = "";
        private string m_strNextMap = "";
        private string m_strNextMode = "";
        private int m_iNextMapIndex = -1;
        private int m_iCurrMapIndex = -1;
        private int m_iCurrentRoundTime = 0;
        private Players m_players = new Players();

        private List<string> m_listOptsDisplay;

        private bool m_boolVotemapEnabled = true;
        private bool m_boolVotingStarted = false;
        private bool m_boolVotingSystemEnabled = false;
        private bool m_boolOnLastRound = true;
        private enumBoolYesNo showNextMapBeforeVote = enumBoolYesNo.No;
        private bool nextmapShown = false;
        private enumBoolYesNo disableVoteResults = enumBoolYesNo.No;


        //string m_strLastMessage = "";

        private DateTime m_timeVoteStart;
        private DateTime m_timeVoteEnd;
        private int m_iEndTimeLeeway = 60;  // seconds
        private TimeSpan m_tsStartVote = TimeSpan.FromHours(1);

        private DateTime m_timePrevious = DateTime.Now;
        private int[] m_iPrevTicket;
        private int ticketCounter = 100;
        private int currentMcom = 0;

        private Dictionary<char, double> m_dictLetterSizes = new Dictionary<char, double>();

        private enumBoolYesNo syncreservedslots = enumBoolYesNo.Yes;
        private List<string> vips = new List<string>();
        private int vipvotecount = 3;

        private enumBoolYesNo confirmpub = enumBoolYesNo.No;
        private string pubconfirmmsg = "%pn% voted for %map%";

        private enumBoolYesNo Check4Update = enumBoolYesNo.Yes;

        private Dictionary<string, int> myvotes = new Dictionary<string, int>();
        //private Dictionary<string, int> myvipvotes = new Dictionary<string, int>();

        //int numteams = 2;

        private List<string> VoteBanner = new List<string>() { "%%%%%%%%%%%%%%%%%%%%%%%", "%%%%%%%%%%%%%%%%%%%%%%%", "%%%%       VOTE NEXT MAP       %%%%", "%%%%%%%%%%%%%%%%%%%%%%%", "%%%%%%%%%%%%%%%%%%%%%%%", "%%%%%%%%%%%%%%%%%%%%%%%", "%%%%%%%%%%%%%%%%%%%%%%%", "%%%%       VOTE NEXT MAP       %%%%", "%%%%%%%%%%%%%%%%%%%%%%%", "%%%%%%%%%%%%%%%%%%%%%%%" };

        private string YellVoteBanner = "Vote Next Map Now!";

        private enumBoolYesNo SayVoteResult = enumBoolYesNo.Yes;
        private enumBoolYesNo YellVoteResult = enumBoolYesNo.No;
        private enumBoolYesNo SayNextMap = enumBoolYesNo.Yes;
        private enumBoolYesNo YellNextMap = enumBoolYesNo.No;

        private enumBoolYesNo displayvotescount = enumBoolYesNo.Yes;

        // Logging to EventLogger
        private enumBoolYesNo logToEventLogger = enumBoolYesNo.No;

        //Addon - block spectator - src: AdKats
        public enum PlayerType
        {
            Player,
            Spectator,
            CommanderPC,
            CommanderMobile
        }

        private class Players
        {
            private List<Player> m_listPlayers = new List<Player>();

            public void UpdatePlayer(CPlayerInfo player)
            {
                bool updated = false;
                foreach (Player p in m_listPlayers)
                {
                    if (p.SoldierName == player.SoldierName)
                    {
                        p.TeamID = player.TeamID;
                        p.SquadID = player.SquadID;
                        p.Type = player.Type;
                        updated = true;
                        break;
                    }
                }
                if (!updated)
                {
                    Add(player.SoldierName, player.TeamID, player.SquadID, player.Type);
                }
            }

            public bool SetVote(string name, int vote)
            {
                foreach (Player p in m_listPlayers)
                {
                    if (p.SoldierName == name)
                    {
                        p.Vote = vote;
                        return true;
                    }
                }
                return false;
            }

            public Player GetPlayer(string name)
            {
                foreach (Player p in m_listPlayers)
                {
                    if (p.SoldierName == name)
                    {
                        return p;
                    }
                }
                return null;
            }

            public List<Squad> GetNonvotedSquads()
            {
                List<Squad> squads = new List<Squad>();

                foreach (Player p in m_listPlayers)
                {
                    if (p.Vote == -1 && !(p.TeamID == 0 && p.SquadID == 0))
                    {
                        bool found = false;
                        foreach (Squad s in squads)
                        {
                            if (s.TeamID == p.TeamID && s.SquadID == p.SquadID)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            squads.Add(new Squad(p.TeamID, p.SquadID));
                        }
                    }
                }

                squads.Sort();

                return squads;
            }

            public void Add(string name, int teamId, int squadId, int Type)
            {
                if (!this.Contains(name))
                {
                    m_listPlayers.Add(new Player(name, teamId, squadId, Type));
                }
            }

            public void Remove(string name)
            {
                foreach (Player p in m_listPlayers)
                {
                    if (p.SoldierName == name)
                    {
                        m_listPlayers.Remove(p);
                    }
                }
            }

            public int Count
            {
                get { return m_listPlayers.Count; }
            }

            public bool Contains(string name)
            {
                foreach (Player p in m_listPlayers)
                {
                    if (p.SoldierName == name)
                    {
                        return true;
                    }
                }
                return false;
            }

            public class Player
            {
                private string m_Name = "";
                private int m_TeamId = -1;
                private int m_SquadId = -1;
                private int m_Vote = -1;
                private int m_Type = -1;

                public Player(string name, int teamId, int squadId, int type)
                {
                    m_Name = name;
                    m_TeamId = teamId;
                    m_SquadId = squadId;
                    m_Vote = -1;
                    m_Type = type;
                }

                public string SoldierName
                {
                    get { return m_Name; }
                    private set { m_Name = value; }
                }

                public int TeamID
                {
                    get { return m_TeamId; }
                    set { m_TeamId = value; }
                }

                public int SquadID
                {
                    get { return m_SquadId; }
                    set { m_SquadId = value; }
                }

                public int Vote
                {
                    get { return m_Vote; }
                    set { m_Vote = value; }
                }

                public int Type
                {
                    get { return m_Type; }
                    set { m_Type = value; }
                }
            }

            public class Squad : IComparable<Squad>
            {
                private int m_TeamId = -1;
                private int m_SquadId = -1;
                public Squad(int team, int squad)
                {
                    m_TeamId = team;
                    m_SquadId = squad;
                }

                public int TeamID
                {
                    get { return m_TeamId; }
                }

                public int SquadID
                {
                    get { return m_SquadId; }
                }

                public int CompareTo(Squad other)
                {
                    if (this.TeamID == other.TeamID)
                    {
                        return this.SquadID.CompareTo(other.SquadID);
                    }
                    return this.TeamID.CompareTo(other.TeamID);
                }
            }
        }

        public xVotemap()
        {
            #region Char Dictionary
            m_strHosVotePrefix = @"/";
            m_dictLetterSizes.Add(' ', 0.50);
            m_dictLetterSizes.Add('a', 0.90);
            m_dictLetterSizes.Add('b', 0.90);
            m_dictLetterSizes.Add('c', 0.78);
            m_dictLetterSizes.Add('d', 0.90);
            m_dictLetterSizes.Add('e', 0.90);
            m_dictLetterSizes.Add('f', 0.56);
            m_dictLetterSizes.Add('g', 0.90);
            m_dictLetterSizes.Add('h', 0.90);
            m_dictLetterSizes.Add('i', 0.40);
            m_dictLetterSizes.Add('j', 0.50);
            m_dictLetterSizes.Add('k', 0.86);
            m_dictLetterSizes.Add('l', 0.40);
            m_dictLetterSizes.Add('m', 1.40);
            m_dictLetterSizes.Add('n', 0.90);
            m_dictLetterSizes.Add('o', 0.90);
            m_dictLetterSizes.Add('p', 0.90);
            m_dictLetterSizes.Add('q', 0.90);
            m_dictLetterSizes.Add('r', 0.62);
            m_dictLetterSizes.Add('s', 0.76);
            m_dictLetterSizes.Add('t', 0.55);
            m_dictLetterSizes.Add('u', 0.90);
            m_dictLetterSizes.Add('v', 0.85);
            m_dictLetterSizes.Add('w', 1.28);
            m_dictLetterSizes.Add('x', 0.82);
            m_dictLetterSizes.Add('y', 0.85);
            m_dictLetterSizes.Add('z', 0.78);
            m_dictLetterSizes.Add('A', 1.00);
            m_dictLetterSizes.Add('B', 1.00);
            m_dictLetterSizes.Add('C', 1.00);
            m_dictLetterSizes.Add('D', 1.12);
            m_dictLetterSizes.Add('E', 1.00);
            m_dictLetterSizes.Add('F', 0.90);
            m_dictLetterSizes.Add('G', 1.18);
            m_dictLetterSizes.Add('H', 1.12);
            m_dictLetterSizes.Add('I', 0.62);
            m_dictLetterSizes.Add('J', 0.70);
            m_dictLetterSizes.Add('K', 1.00);
            m_dictLetterSizes.Add('L', 0.85);
            m_dictLetterSizes.Add('M', 1.32);
            m_dictLetterSizes.Add('N', 1.11);
            m_dictLetterSizes.Add('O', 1.20);
            m_dictLetterSizes.Add('P', 0.92);
            m_dictLetterSizes.Add('Q', 1.20);
            m_dictLetterSizes.Add('R', 1.05);
            m_dictLetterSizes.Add('S', 0.91);
            m_dictLetterSizes.Add('T', 1.00);
            m_dictLetterSizes.Add('U', 1.12);
            m_dictLetterSizes.Add('V', 1.00);
            m_dictLetterSizes.Add('W', 1.55);
            m_dictLetterSizes.Add('X', 1.00);
            m_dictLetterSizes.Add('Y', 1.00);
            m_dictLetterSizes.Add('Z', 0.92);
            #endregion
        }

        #endregion

        #region PluginSetup

        public string GetPluginName()
        {
            return "xVotemap";
        }

        public string GetPluginVersion()
        {
            return "1.5.10";
        }

        public string GetPluginAuthor()
        {
            return "Hedius(E4GL), <= 1.5.6.0 onegrizzlybeer/grizzlybeer, Hexacanon EG modification, Hand of Shadow, versions <=1.3.1 by aether";
        }

        public string GetPluginWebsite()
        {
            return "gitlab.com/e4gl/xVotemap";
        }

        public string GetPluginDescription()
        {
            return @"
        <h2>Description</h2>
        <p>Calls an in-game vote for the next map near the end on the round. Randomly selects four maps (by default, can be adjusted) from the current maplist to vote on. Before the options are displayed, a banner can be shown to get the players attention that voting is about to commence.<br>

        The votes are tallied and if there are more votes than the set threshold, the winner is the map with the most votes. If there is a tie, the winner is selected at random from the tied maps. The next map is announced and it can be periodically displayed until the end of the round.</p>

        <h2>In-game Commands</h2><br>

        <blockquote> 
        <h4>/#</h4>While a vote is in progress, votes for the option represented by the #. / may be replaced with a different prefix.
        </blockquote>

        <blockquote> 
        <h4>/v, !v</h4>Displays the vote options while a vote is in progress. If typed before a vote, it will display the predicted amount of time before the vote begins.
        </blockquote>

        <blockquote> 
        <h4>/nextmap, !nextmap</h4>Displays the next map.
        </blockquote>

        <blockquote> 
        <h4>/votemap, !votemap</h4>Initiates/restarts a votemap if the player has map changing priviledges in Procon.
        </blockquote>

        <blockquote> 
        <h4>enablevotemap</h4>Enables the votemap plugin if the player has map changing priviledges in Procon.
        </blockquote>

        <blockquote> 
        <h4>disablevotemap</h4>Disables the votemap plugin if the player has map changing priviledges in Procon.
        </blockquote>

        <h2>Settings</h2><br>

        <h3>Display</h3><br>

        <blockquote> 
        <h4>Enable Vote Banner?</h4>Is the banner (designed to get the attention of players) displayed before the first voting options list (First), before every map option display (All), or never displayed (disabled).
        </blockquote>

        <blockquote> 
        <h4>Show Gamemode in Voting Options?</h4> Determines if the shorthand notation for the gamemode is displayed in the voting options. This is most useful for those running mixed mode servers, especially if the same map is in the maplist with different gamemodes.
        </blockquote>

        <blockquote> 
        <h4>Show Nextmap Before Vote?</h4> If 'yes', then the default next map is shown before the vote options so people can decide if they want to vote or not.
        </blockquote>

        <blockquote> 
        <h4>Voting Options Interval</h4>Integer, in seconds, representing how often the map vote options are displayed once the voting poll opens.
        </blockquote>

        <blockquote> 
        <h4>Next Map Display Interval</h4>Integer, in seconds, representing how often the winning next map is displayed after the voting is over but the round hasn't ended yet. A value of [b]-1[/b] will disable nextmap messages.
        </blockquote>

        <blockquote> 
        <h4>Disable Vote Results Display?</h4>If 'yes' then the results of the vote will NOT be displayed. It will still say when voting ends or if the vote failed.
        </blockquote>
		<blockquote> 
        <h4>Uservote Prefix</h4>Prefix character for the voting number. !!! USE ONLY one character  e.g /;@;*;#...
        </blockquote>

        <h3>Map Options</h3><br>
        
        <blockquote> 
        <h4>Number of Vote Options</h4>Integer, between 2 and 8, representing the number of vote options to be displayed when a mapvote is called.
        </blockquote>

        <blockquote> 
        <h4>Exclude Current Map From  Vote Options?</h4>If yes, the current map with be excluded from the next map voting options, so to not allow the same map to be played twice.
        </blockquote>

        <blockquote> 
        <h4>Sort the Maplist by:</h4>If 'Map Only' is selected, when a map is played all the gametypes for that map will be pushed to the end of the list. 'Map and Gametype' will only push that particular map and gametpye to the end of the list. 'Gametype Only' will push all maps with that gametype to the end of the list.
        </blockquote>

        <blockquote> 
        <h4>Randomness</h4>An integer between 0 and 10 representing the randomness of the map options for a vote. The algorithm changes the probability of each map and gamemode pair being selected as a vote option depending on how long ago it was last played.<br>
        eg. If the maplist has 13 maps and number of vote options is set to 4. The maplist is sorted from played a long time ago (1) to played last map (13).<br>
        <img border=""0"" src=""http://i.imgur.com/kCvSK.png""><br>
        Randomness = 0: Will rotate through the maplist, only showing the maps that haven't been played in a long time. Players should end up playing every map in the cycle.<br><br>
        <img border=""0"" src=""http://i.imgur.com/fZPGC.png""><br><br>
        <img border=""0"" src=""http://i.imgur.com/CmULh.png""><br>
        Randomness = 5: Maps that haven't been played in a long time will have the highest probablity and recently played maps will have the lowest probability of being selected as vote options.<br><br>
        <img border=""0"" src=""http://i.imgur.com/fHtpu.png""><br><br>
        <img border=""0"" src=""http://i.imgur.com/nlYTd.png""><br>
        Randomness = 10: Every map has the same chance of being selected as an option. Players will tend to play the most popular maps repeatedly.<br><br>
        </blockquote>

        <h3>Voting</h3><br>      

        <blockquote> 
        <h4>Trigger</h4>If Automatic, the voting will be triggered automatically near the end of the round. If Manual, the voting will have to be triggered manually with the /votemap command.
        </blockquote>

        <blockquote> 
        <h4>Voting Threshold</h4>Integer, representing the minimum total number of votes that must cast for the highest voted map to become the nextmap. If no map reaches this threshold, then the map will just roll over to the next map in the maplist.
        </blockquote>

        <blockquote> 
        <h4>Voting Duration</h4>Integer, in seconds, representing the target length of time the voting poll is open for.
        </blockquote>

        <blockquote> 
        <h4>Time between Voting End and Round End (Conquest)</h4>Integer, in seconds, representing how long from the end of the round the voting will finish. In other words, at what time you want the voting to take place, mear seconds before the end of round or in the middle of round sometime.
        </blockquote>

        <blockquote> 
        <h4>Voting Start Time from Start of Round (Rush and Defuse (Elimination))</h4>Integer, representing the time when voting will start, in seconds from the start of the round. In rush, there is no way to predict when the round will finish with the current RCON information, as the status of MCOMs are unknown. So you must estimate how long the shortest rush round would take and start the vote a couple of minutes earlier in order to ensure a vote will take place.
        </blockquote>

        <blockquote> 
        <h4>Minimum Players for Start</h4>Integer, representing the minimum number for players required in the server at the time of vote for the vote to start.
        </blockquote>

        <blockquote> 
        <h4>Maximum Players for Start</h4>Integer, representing the maximum number for players allowed in the server at the time of vote for the vote to start.
        </blockquote>

        <h3>Xtras</h3><br>

        <blockquote> 
        <h4>Debug Level</h4>Integer between 0 and 5, where 0 outputs no plugin debug messages, and 5 which outputs even the most mundane steps.
        </blockquote>

        <blockquote> 
        <h4>Log to EventLogger</h4>Boolean: Log to EventLogger
        </blockquote>


        <h2>Development</h2><br>
        
        <h3>Known issues BF3/BF4</h3><br>
        <ul>
        <li>If the maplist is changed during a vote the winning map will not correctly be the next map.</li>
        </ul>

        <h3>Known issues BF4</h3><br>
        <ul>
        <li>For TDM the gameserver reports 4 teams. (There are only 2)</li>        
        </ul>

        <h3>Future Work</h3><br>
        <ul>
        <li>Improve Auto Triggers</li>
        <li>Improve map sorting algorithm</li>
        <li>Add maplist change event</li>
        </ul>

        <h3>Change Log</h3><br>
        <h4>1.5.8.0</h4><br>
        <ul>
        <li>New option: Block spectators</li>
        <li>Display options with !v and /v</li>
        </ul> 
        <h4>1.5.7.0</h4><br>
        <ul>
        <li>Added !nextmap</li>
        </ul> 
        <h4>1.5.6.0</h4><br>
        <ul>
        <li>NEW: New gamemode Chain Link (Dragon's Teeth DLC) is now supported.</li>
        </ul> 
        <h4>1.5.5.1</h4><br>
        <ul>
        <li>Fix: added missing gamemode [CA] (CarrierAssaultSmall0) to vote options.</li>
        </ul> 
        <h4>1.5.5.0</h4><br>
        <ul>
        <li>NEW: New gamemode Carrier Assault (Naval Strike DLC) is now supported</li>
        <li>Fix: Next map display on round over scoreboard limited to last round</li>
        <li>Fix: vote threshold display did not take vipvotes into account</li>
        </ul> 
        <h4>1.5.4.2</h4><br>
        <ul>
        <li>Fix: Restarting a running vote did not properly reset the votes</li>
        </ul> 
        <h4>1.5.4.1</h4><br>
        <ul>
        <li>NEW: Options (say, yell) to control the display of the next map/winning map</li>
        <li>Fix: Added next map display when the scoreboard is displayed (THX to Hand of Shadow)</li>
        <li>Fix: Changed Include/Exclude VIPs to Sync VIPs</li>
        </ul>         
        <h4>1.5.4.0</h4><br>
        <ul>        
        <li>NEW: Automatic Update Check</li>        
        <li>NEW: customizable votebanner</li>        
        <li>NEW: Show number of votes for each map</li>
        <li>NEW: exclude current gamemode from vote options</li>
        <li>NEW: time limit effective for all gamemodes (bf4 round time limit)</li>
        <li>Fix: increase possible number of vote options</li>        
        <li>Fix: fixed error removing duplicates</li>        
        <li>Fix: Version Number</li>
        </ul> 
        <h4>1.5.3</h4><br>
        <ul>
        <li>FIX: EXCEPTION CAUGHT IN: SetVoteStartAndEndTimes is now fixed (for all gamemodes)</li>
		<li>FIX: Changed some messages to better reflect the current status</li>
        <li>FIX: If the gameserver (for whatever reason) reports total-rounds = 0, votemap will asume total-rounds = 1. This should fix a bug some users are having. (votemap just not running, saying 'not next round')</li>
        </ul> 
        <h4>1.5.2</h4><br>
        <ul>
        <li>FIX: EXCEPTION CAUGHT IN: SetVoteStartAndEndTimes is now fixed</li>
		<li>FIX: increased max players to start to 70</li>
		<li>NEW: Dynamic prefix vote options. THX to Hand of Shadow</li>
        </ul> 
        <h4>1.5.1</h4><br>
        <ul>
        <li>FIX: Short Tags for BF4 GameModes. Thx BFTALON</li>
        </ul> 
        <h4>1.5</h4><br>
        <ul>
        <li>NEW: BF4 Support</li>
        </ul> 
        <h4>1.4.4</h4><br>
        <ul>
        <li>FIX: too many reservedSlots (ServerVIPs) requests</li>
        <li>NEW: some things i forgot :D</li>
        </ul> 
        <h4>1.4.3</h4><br>
        <ul>
        <li>Added: End Game Modes (CTF)</li>
        </ul> 
        <h4>1.4.2</h4><br>
        <ul>
        <li>Fix: Public confirmation message was not updating correctly</li>
        <li>Fix: Removal of duplicates in vote options was not working under certain circumstances</li>
        </ul> 
        <h4>1.4.1</h4><br>
        <ul>
        <li>Added: Aftermath Modes (Scavenger)</li>
        <li>Added: Option to enable/disable auto including ServerVIPs/ReservedSlots in VIP List</li>
        </ul> 
        <h4>1.4.0</h4><br>
        <ul>
        <li>Added: VIP List</li>
        <li>Added: VIP VoteCount</li>
        <li>Fixed: show maps only once if they are double in maplist.</li>
        <li>Fixed: GameModes</li>
        </ul>          
        <h4>1.3.1</h4><br>
        <ul>
        <li>Fixed: /votemap to start a map vote.</li>
        </ul>
        <h4>1.3.0</h4><br>
        <ul>
        <li>Added: Yell announcement.</li>
        <li>Added: In-game enable and disable.</li>
        </ul>
        <h4>1.2.3</h4><br>
        <ul>
        <li>Fixed: Mapnames fixed from patch.</li>
        </ul>
        <h4>1.2.2</h4><br>
        <ul>
        <li>Fixed: Maplist sort for 'Map Only' and 'Gametype Only'. Will still work on a better algorithm too.</li>
        </ul>
        <h4>1.2.1</h4><br>
        <ul>
        <li>Added: Information link with Ultimate Map Manager to ensure the voted map will be the next map if the maplist needs to change and it is still in the maplist.</li>
        <li>Fixed: Remove current map bug.</li>
        <li>Added: Improved map options display. Should reduce the chance of very long options.</li>
        <li>Fixed: Gamemode should appear throughout the in-game message, if turned on.</li>
        </ul>
        <h4>1.2.0</h4><br>
        <ul>
        <li>Added: Ability to disable vote results from being displayed.</li>
        <li>Added: Option to change the way the maplist is sorted.</li>
        <li>Added: Option to display the nextmap before the vote.</li>
        <li>Added: Option to set minimum and maximum player count limits for if the vote will take place.</li>
        </ul>
        <h4>1.1.5</h4><br>
        <ul>
        <li>Added: More debug messages</li>
        <li>Fix: Serverinfo bug possibly =\</li>
        <li>Change: Renamed to xVotemap. Delete old CxVotemap.</li>
        </ul>
        <h4>1.1.4</h4><br>
        <ul>
        <li>Fix: /votemap command now works on all rounds</li>
        <li>Fix: Between rounds error bug.</li>
        </ul>
        <h4>1.1.3</h4><br>
        <ul>
        <li>Fix: Votemap starting too early.</li>
        </ul>
        <h4>1.1.2</h4><br>
        <ul>
        <li>Fix: Minimal changes</li>
        </ul>
        <h4>1.1.1</h4><br>
        <ul>
        <li>Fix: Several bugs that were in the new randomness code. A lot of console debug text has been added, most will be removed or suppressed when this version is proved stable.</li>
        </ul>
        <h4>1.1.0</h4><br>
        <ul>
        <li>Added: Randomness, a new algorithm to decide which maps are chosen to be vote options.</li>
        <li>Added: Ability to disable votemap starting automatically.</li>
        </ul>
        <h4>1.0.1</h4><br>
        <ul>
        <li>Fixed: Tweaked various algorithms</li>
        <li>Fixed: Squad rush bug</li>
        </ul>
        <h4>1.0.0</h4><br>
        <ul>
        <li>Added: Warning for incorrect vote command</li>
        </ul>
        <h4>0.0.8</h4><br>
        <ul>
        <li>Fix: Exclude current map</li>
        <li>Fix: Change to rush start time estimate. Needs testing again.</li>
        </ul>
        <h4>0.0.7</h4><br>
        <ul>
        <li>Fix: Mixed-mode bug. Needs testing to check if it succeeded.</li>
        <li>Added: Customizable number of vote options</li>
        <li>Added: Option to display the gamemode in the vote options</li>
        <li>Added: Admins with mapchanging privileges can initiate/restart the Votemap in-game with /votemap </li>
        </ul>
        <h4>0.0.6</h4><br>
        <ul>
        <li>Fix: Bug where the last map in the maplist would not be selected.</li>
        <li>Added: /nextmap command</li>
        </ul>
        <h4>0.0.5</h4><br>
        <ul>
        <li>Fixed, Last round bug</li>
        </ul>
        <h4>0.0.4</h4><br>
        <ul>
        <li>Added, 'Exclude current map' as an option in the settings.</li>
        <li>Fixed, xVotemap stopping due to a rush map being detected.</li>
        </ul>
        <h4>0.0.3</h4><br>
        <ul>
        <li>Changed the vote threshold to be compared to the total votes cast, rather than the winning map.</li>
        <li>In the event of a draw, now, a random map out of the drawn maps will be chosen.</li>
        <li>Added Rush and Deathmatch support, expect bugs, please report bugs and provide feedback.</li>
        </ul>
        <h4>0.0.2</h4><br>
        <ul>
        <li>Fix for vote banner not showing.</li>
        </ul>
        <h4>0.0.1</h4><br>
        <ul>
        <li>Initial Beta. Find all of the bugs! <img border=""0"" src=""http://i.imgur.com/88jZM.png"" width=""50"" height=""50""></li>
        </ul>
";
        }

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();
            lstReturn.Add(new CPluginVariable("Display|Enable Vote Banner?", "enum.VoteBanner(Disabled|First|All)", this.m_strEnableVoteBanner));
            if (this.m_strEnableVoteBanner == "First" || this.m_strEnableVoteBanner == "All")
            {
                lstReturn.Add(new CPluginVariable("Display|Banner Type", "enum.VoteBannerType(Chat|Yell|Both)", this.m_strBannerType));

                if (this.m_strBannerType == "Yell" || this.m_strBannerType == "Both")
                {
                    lstReturn.Add(new CPluginVariable("Display|Banner Yell Duration (s)", this.m_iBannerYellDuration.GetType(), this.m_iBannerYellDuration));
                }

                lstReturn.Add(new CPluginVariable("Display|Vote Banner", typeof(string[]), this.VoteBanner.ToArray()));
                lstReturn.Add(new CPluginVariable("Display|Vote Banner Yell", YellVoteBanner.GetType(), this.YellVoteBanner));
            }

            lstReturn.Add(new CPluginVariable("Display|Voting Options Interval (s)", this.m_iVotingOptionsInterval.GetType(), this.m_iVotingOptionsInterval));
            lstReturn.Add(new CPluginVariable("Display|Show Nextmap Before Vote?", typeof(enumBoolYesNo), this.showNextMapBeforeVote));
            lstReturn.Add(new CPluginVariable("Display|Next Map Display Interval (s)", this.m_iNextMapDisplayInterval.GetType(), this.m_iNextMapDisplayInterval));
            lstReturn.Add(new CPluginVariable("Display|Say Next Map?", typeof(enumBoolYesNo), this.SayNextMap));
            lstReturn.Add(new CPluginVariable("Display|Yell Next Map?", typeof(enumBoolYesNo), this.YellNextMap));
            lstReturn.Add(new CPluginVariable("Display|Show Gamemode in Voting Options?", typeof(enumBoolYesNo), this.m_enumShowGamemode));
            lstReturn.Add(new CPluginVariable("Display|Display Number of Votes for each Option", typeof(enumBoolYesNo), this.displayvotescount));
            lstReturn.Add(new CPluginVariable("Display|Disable Vote Results Display?", typeof(enumBoolYesNo), this.disableVoteResults));
            lstReturn.Add(new CPluginVariable("Display|Say Vote Results?", typeof(enumBoolYesNo), this.SayVoteResult));
            lstReturn.Add(new CPluginVariable("Display|Yell Vote Results?", typeof(enumBoolYesNo), this.YellVoteResult));
            lstReturn.Add(new CPluginVariable("Display|Confirm Vote publically?", typeof(enumBoolYesNo), this.confirmpub));
            if (this.confirmpub == enumBoolYesNo.Yes)
                lstReturn.Add(new CPluginVariable("Display|Message to be displayed", this.pubconfirmmsg.GetType(), this.pubconfirmmsg));

            lstReturn.Add(new CPluginVariable("Map Options|Number of Vote Options", this.m_iNumOfMapOptions.GetType(), this.m_iNumOfMapOptions));
            lstReturn.Add(new CPluginVariable("Map Options|Exclude Current Map From Vote Options?", typeof(enumBoolYesNo), this.m_enumExcludeCurrMap));
            lstReturn.Add(new CPluginVariable("Map Options|Exclude Current Mode From Vote Options?", typeof(enumBoolYesNo), this.m_enumExcludeCurrMode));
            lstReturn.Add(new CPluginVariable("Map Options|Sort the Maplist by:", "enum.MapGametype(Map Only|Map and Gametype|Gametype Only)", this.mapSortSettings));
            lstReturn.Add(new CPluginVariable("Map Options|Randomness", this.m_iRandomness.GetType(), this.m_iRandomness));

            lstReturn.Add(new CPluginVariable("Voting|Trigger", "enum.VotingTrigger(Manual|Automatic)", this.m_strTrigger));
            lstReturn.Add(new CPluginVariable("Voting|Uservote prefix", this.m_strHosVotePrefix.GetType(), this.m_strHosVotePrefix));
            lstReturn.Add(new CPluginVariable("Voting|Votes Threshold (votes)", this.m_iVoteThres.GetType(), this.m_iVoteThres));
            lstReturn.Add(new CPluginVariable("Voting|Voting Duration (s)", this.m_iVotingDuration.GetType(), this.m_iVotingDuration));
            lstReturn.Add(new CPluginVariable("Voting|Time between Voting End and Round End (s) (Conquest)", this.m_iStopVoteTime.GetType(), this.m_iStopVoteTime));
            lstReturn.Add(new CPluginVariable("Voting|Voting Start Time from Start of Round (s) (Carrier Assault)", this.m_iCalVoteStartTime.GetType(), this.m_iCalVoteStartTime));
            lstReturn.Add(new CPluginVariable("Voting|Voting Start Time from Start of Round (s) (Rush and Defuse (Elimination))", this.m_iRushVoteStartTime.GetType(), this.m_iRushVoteStartTime));
            lstReturn.Add(new CPluginVariable("Voting|Minimum Players for Start", this.minimumPlayers.GetType(), this.minimumPlayers));
            lstReturn.Add(new CPluginVariable("Voting|Maximum Players for Start", this.maximumPlayers.GetType(), this.maximumPlayers));
            lstReturn.Add(new CPluginVariable("Voting|Allow Spectators to Vote?", this.m_enumAllowSpectatorsToVote.GetType(), this.m_enumAllowSpectatorsToVote));

            lstReturn.Add(new CPluginVariable("Xtras|Debug Level", this.m_iDebugLevel.GetType(), this.m_iDebugLevel));
            lstReturn.Add(new CPluginVariable("Xtras|Vip list", typeof(string[]), this.vips.ToArray()));
            lstReturn.Add(new CPluginVariable("Xtras|Sync ServerVIPs/ReservedSlots", typeof(enumBoolYesNo), this.syncreservedslots));
            lstReturn.Add(new CPluginVariable("Xtras|VIP Vote Count", vipvotecount.GetType(), vipvotecount));
            lstReturn.Add(new CPluginVariable("Xtras|Log to EventLogger?", logToEventLogger.GetType(), logToEventLogger));

            lstReturn.Add(new CPluginVariable("Xtras|Check for Update?", this.Check4Update.GetType(), this.Check4Update));

            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();

            lstReturn.Add(new CPluginVariable("Enable Vote Banner?", "enum.VoteBanner(Disabled|First|All)", this.m_strEnableVoteBanner));
            lstReturn.Add(new CPluginVariable("Banner Type", "enum.VoteBannerType(Chat|Yell|Both)", this.m_strBannerType));
            lstReturn.Add(new CPluginVariable("Banner Yell Duration (s)", this.m_iBannerYellDuration.GetType(), this.m_iBannerYellDuration));
            lstReturn.Add(new CPluginVariable("Vote Banner", typeof(string[]), this.VoteBanner.ToArray()));
            lstReturn.Add(new CPluginVariable("Vote Banner Yell", YellVoteBanner.GetType(), this.YellVoteBanner));
            lstReturn.Add(new CPluginVariable("Voting Options Interval (s)", this.m_iVotingOptionsInterval.GetType(), this.m_iVotingOptionsInterval));
            lstReturn.Add(new CPluginVariable("Show Nextmap Before Vote?", typeof(enumBoolYesNo), this.showNextMapBeforeVote));
            lstReturn.Add(new CPluginVariable("Next Map Display Interval (s)", this.m_iNextMapDisplayInterval.GetType(), this.m_iNextMapDisplayInterval));
            lstReturn.Add(new CPluginVariable("Say Next Map?", typeof(enumBoolYesNo), this.SayNextMap));
            lstReturn.Add(new CPluginVariable("Yell Next Map?", typeof(enumBoolYesNo), this.YellNextMap));
            lstReturn.Add(new CPluginVariable("Show Gamemode in Voting Options?", typeof(enumBoolYesNo), this.m_enumShowGamemode));
            lstReturn.Add(new CPluginVariable("Display Number of Votes for each Option", typeof(enumBoolYesNo), this.displayvotescount));
            lstReturn.Add(new CPluginVariable("Disable Vote Results Display?", typeof(enumBoolYesNo), this.disableVoteResults));
            lstReturn.Add(new CPluginVariable("Say Vote Results?", typeof(enumBoolYesNo), this.SayVoteResult));
            lstReturn.Add(new CPluginVariable("Yell Vote Results?", typeof(enumBoolYesNo), this.YellVoteResult));
            lstReturn.Add(new CPluginVariable("Confirm Vote publically?", typeof(enumBoolYesNo), this.confirmpub));
            lstReturn.Add(new CPluginVariable("Message to be displayed", this.pubconfirmmsg.GetType(), this.pubconfirmmsg));

            lstReturn.Add(new CPluginVariable("Number of Vote Options", this.m_iNumOfMapOptions.GetType(), this.m_iNumOfMapOptions));
            lstReturn.Add(new CPluginVariable("Exclude Current Map From Vote Options?", typeof(enumBoolYesNo), this.m_enumExcludeCurrMap));
            lstReturn.Add(new CPluginVariable("Exclude Current Mode From Vote Options?", typeof(enumBoolYesNo), this.m_enumExcludeCurrMode));
            lstReturn.Add(new CPluginVariable("Sort the Maplist by:", "enum.MapGametype(Map Only|Map and Gametype|Gametype Only)", this.mapSortSettings));
            lstReturn.Add(new CPluginVariable("Randomness", this.m_iRandomness.GetType(), this.m_iRandomness));

            lstReturn.Add(new CPluginVariable("Trigger", "enum.VotingTrigger(Manual|Automatic)", this.m_strTrigger));
            lstReturn.Add(new CPluginVariable("Uservote prefix", this.m_strHosVotePrefix.GetType(), this.m_strHosVotePrefix));
            lstReturn.Add(new CPluginVariable("Votes Threshold (votes)", this.m_iVoteThres.GetType(), this.m_iVoteThres));
            lstReturn.Add(new CPluginVariable("Voting Duration (s)", this.m_iVotingDuration.GetType(), this.m_iVotingDuration));
            lstReturn.Add(new CPluginVariable("Time between Voting End and Round End (s) (Conquest)", this.m_iStopVoteTime.GetType(), this.m_iStopVoteTime));
            lstReturn.Add(new CPluginVariable("Voting Start Time from Start of Round (s) (Carrier Assault)", this.m_iCalVoteStartTime.GetType(), this.m_iCalVoteStartTime));
            lstReturn.Add(new CPluginVariable("Voting Start Time from Start of Round (s) (Rush and Defuse (Elimination))", this.m_iRushVoteStartTime.GetType(), this.m_iRushVoteStartTime));
            lstReturn.Add(new CPluginVariable("Minimum Players for Start", this.minimumPlayers.GetType(), this.minimumPlayers));
            lstReturn.Add(new CPluginVariable("Maximum Players for Start", this.maximumPlayers.GetType(), this.maximumPlayers));
            lstReturn.Add(new CPluginVariable("Allow Spectators to Vote?", this.m_enumAllowSpectatorsToVote.GetType(), this.m_enumAllowSpectatorsToVote));

            lstReturn.Add(new CPluginVariable("Debug Level", this.m_iDebugLevel.GetType(), this.m_iDebugLevel));
            lstReturn.Add(new CPluginVariable("Vip list", typeof(string[]), this.vips.ToArray()));
            lstReturn.Add(new CPluginVariable("Sync ServerVIPs/ReservedSlots", typeof(enumBoolYesNo), this.syncreservedslots));
            lstReturn.Add(new CPluginVariable("VIP Vote Count", vipvotecount.GetType(), vipvotecount));
            lstReturn.Add(new CPluginVariable("Log to EventLogger?", logToEventLogger.GetType(), logToEventLogger));


            lstReturn.Add(new CPluginVariable("Check for Update?", this.Check4Update.GetType(), this.Check4Update));

            return lstReturn;
        }

        public void SetPluginVariable(string strVariable, string strValue)
        {
            int iValue = 0;

            if (strVariable.CompareTo("Sync ServerVIPs/ReservedSlots") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.syncreservedslots = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Say Next Map?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.SayNextMap = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Yell Next Map?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.YellNextMap = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Say Vote Results?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.SayVoteResult = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Yell Vote Results?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.YellVoteResult = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Confirm Vote publically?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.confirmpub = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Message to be displayed") == 0)
            {
                this.pubconfirmmsg = strValue;
            }
            else if (strVariable.CompareTo("Exclude Current Map From Vote Options?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.m_enumExcludeCurrMap = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Exclude Current Mode From Vote Options?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.m_enumExcludeCurrMode = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Randomness") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iRandomness = iValue;

                if (iValue < 0)
                {
                    this.m_iRandomness = 0;
                }
                else if (iValue > 10)
                {
                    this.m_iRandomness = 10;
                }
            }
            else if (strVariable.CompareTo("Sort the Maplist by:") == 0)
            {
                this.mapSortSettings = strValue;
            }
            else if (strVariable.CompareTo("Number of Vote Options") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iNumOfMapOptions = iValue;

                if (iValue < 2)
                {
                    this.m_iNumOfMapOptions = 2;
                }
                else if (iValue > 14)
                {
                    this.m_iNumOfMapOptions = 14;
                }
            }
            else if (strVariable.CompareTo("Voting Duration (s)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iVotingDuration = iValue;

                if (iValue < 30)
                {
                    this.m_iVotingDuration = 30;
                }
            }
            else if (strVariable.CompareTo("Time between Voting End and Round End (s) (Conquest)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iStopVoteTime = iValue;

                if (iValue < 90)
                {
                    this.m_iStopVoteTime = 90;
                }
            }
            else if (strVariable.CompareTo("Voting Options Interval (s)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iVotingOptionsInterval = iValue;

                if (iValue < 10)
                {
                    this.m_iVotingOptionsInterval = 10;
                }
            }
            if (strVariable.CompareTo("Show Nextmap Before Vote?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.showNextMapBeforeVote = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Next Map Display Interval (s)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iNextMapDisplayInterval = iValue;

                if (iValue < -1)
                {
                    this.m_iNextMapDisplayInterval = -1;
                }
            }
            else if (strVariable.CompareTo("Trigger") == 0)
            {
                this.m_strTrigger = strValue;
            }
            else if (strVariable.CompareTo("Votes Threshold (votes)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iVoteThres = iValue;

                if (iValue < 1)
                {
                    this.m_iVoteThres = 0;
                }
                else if (iValue > 64)
                {
                    this.m_iVoteThres = 64;
                }
            }
            else if (strVariable.CompareTo("Voting Start Time from Start of Round (s) (Rush and Defuse (Elimination))") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iRushVoteStartTime = iValue;

                if (iValue < 1)
                {
                    this.m_iRushVoteStartTime = 1;
                }
            }
            else if (strVariable.CompareTo("Voting Start Time from Start of Round (s) (Carrier Assault)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                this.m_iCalVoteStartTime = iValue;

                if (iValue < 1)
                {
                    this.m_iCalVoteStartTime = 1;
                }
            }
            else if (strVariable.CompareTo("Minimum Players for Start") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                minimumPlayers = iValue;
            }
            else if (strVariable.CompareTo("Maximum Players for Start") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                maximumPlayers = iValue;
            }
            else if (strVariable.CompareTo("Allow Spectators to Vote?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.m_enumAllowSpectatorsToVote = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Enable Vote Banner?") == 0)
            {
                this.m_strEnableVoteBanner = strValue;
            }
            else if (strVariable.CompareTo("Banner Type") == 0)
            {
                this.m_strBannerType = strValue;
            }
            else if (strVariable.CompareTo("Banner Yell Duration (s)") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                m_iBannerYellDuration = iValue;
            }
            else if (strVariable.CompareTo("Show Gamemode in Voting Options?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.m_enumShowGamemode = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Disable Vote Results Display?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.disableVoteResults = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Debug Level") == 0 && int.TryParse(strValue, out iValue) == true)
            {
                m_iDebugLevel = iValue;
            }
            else if (strVariable.CompareTo("Vip list") == 0)
            {
                this.vips = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }
            else if (strVariable.CompareTo("Vote Banner Yell") == 0)
            {
                this.YellVoteBanner = strValue;
            }
            else if (strVariable.CompareTo("Vote Banner") == 0)
            {
                this.VoteBanner = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }
            else if (strVariable.CompareTo("VIP Vote Count") == 0)
            {
                int valueAsInt;
                int.TryParse(strValue, out valueAsInt);
                if (valueAsInt >= 1)
                {
                    vipvotecount = valueAsInt;
                }
                else
                {
                    vipvotecount = 1;
                }
            }
            else if (strVariable.CompareTo("Uservote prefix") == 0)
            {
                this.m_strHosVotePrefix = strValue;
            }
            else if (strVariable.CompareTo("Check for Update?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.Check4Update = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Display Number of Votes for each Option") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.displayvotescount = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }
            else if (strVariable.CompareTo("Log to EventLogger?") == 0 && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
            {
                this.logToEventLogger = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
            }



        }

        #endregion

        #region Procon Events

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.m_strHostName = strHostName;
            this.m_strPort = strPort;
            this.m_strPRoConVersion = strPRoConVersion;
            this.RegisterEvents(this.GetType().Name, "OnListPlayers", "OnPlayerLeft", "OnServerInfo", "OnGameModeCounter", "OnMaplistList", "OnMaplistGetMapIndices", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnRoundOver", "OnLevelLoaded", "OnRestartLevel", "OnEndRound", "OnRunNextLevel", "OnReservedSlotsList", "OnRoundOverTeamScores");
        }

        public void OnPluginEnable()
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", "^bxVotemap ^2Enabled!");

            StartVotingSystem();
        }

        public void OnPluginDisable()
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", "^bxVotemap ^1Disabled =(");

            StopVotingSystem();
        }

        public void OnListPlayers(List<CPlayerInfo> lstPlayers, CPlayerSubset cpsSubset)
        {
            this.m_iCurrPlayerCount = lstPlayers.Count;
            foreach (CPlayerInfo player in lstPlayers)
            {
                m_players.UpdatePlayer(player);
            }
            WritePluginConsole("There are " + m_players.Count + " players in the db.", "Info", 5);
        }

        public void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            m_players.Remove(playerInfo.SoldierName);
            WritePluginConsole(playerInfo.SoldierName + " removed from the db.", "Info", 5);
        }

        public void OnServerInfo(CServerInfo csiServerInfo)
        {
            DateTime timehelper = lastcheck.AddSeconds(59.0);
            if (DateTime.Compare(timehelper, DateTime.Now) <= 0)
            {
                this.ExecuteCommand("procon.protected.send", "reservedSlotsList.list");
                WritePluginConsole("sent reservedSlotsList.list", "Info", 4);
                lastcheck = DateTime.Now;
            }
            try
            {
                if (this.m_timeServerInfoCheck < DateTime.Now && m_boolVotingSystemEnabled)
                {
                    WritePluginConsole("ServerInfo updating.", "Info", 5);
                    this.m_iCurrentRoundTime = csiServerInfo.RoundTime;
                    this.m_strCurrentMap = csiServerInfo.Map;
                    this.m_strCurrentGameMode = csiServerInfo.GameMode;

                    //WritePluginConsole(csiServerInfo.TeamScores.Count.ToString(), "Info", 3);

                    this.m_listCurrTeamScore = new List<TeamScore>(csiServerInfo.TeamScores);

                    //this.m_listCurrTeamScore = new List<TeamScore>();
                    //m_listCurrTeamScore.Add(new TeamScore(1, 11));
                    //m_listCurrTeamScore.Add(new TeamScore(2, 22));
                    //this.m_iCurrPlayerCount = csiServerInfo.PlayerCount;

                    //numteams = m_listCurrTeamScore.Count;
                    //if (numteams < 2)
                    //{
                    //    numteams = 2;
                    //}

                    if (this.m_listCurrTeamScore.Count != 0 && (this.m_iPrevTicket == null || this.m_iPrevTicket.Length == 0))
                    {
                        this.m_iPrevTicket = new int[this.m_listCurrTeamScore.Count];
                        for (int i = 0; i < this.m_listCurrTeamScore.Count; i++)
                        {
                            this.m_iPrevTicket[i] = -1;
                        }
                    }
                    //if (this.m_iPrevTicket == null || this.m_iPrevTicket.Length == 0)
                    //{
                    //    this.m_iPrevTicket = new int[numteams];
                    //    for (int i = 0; i < numteams; i++)
                    //    {
                    //        this.m_iPrevTicket[i] = -1;
                    //    }
                    //}

                    if (m_boolVotingSystemEnabled && (csiServerInfo.CurrentRound + 1 < csiServerInfo.TotalRounds && csiServerInfo.TotalRounds != 0))
                    {
                        this.m_boolOnLastRound = false;
                        StopVotingSystem();
                        WritePluginConsole("Not last round. Stopping voting system.", "Info", 3);
                        WritePluginConsole("Rounds played: " + csiServerInfo.CurrentRound, "Info", 5);
                        WritePluginConsole("Total rounds: " + csiServerInfo.TotalRounds, "Info", 5);
                    }

                    SetVoteStartAndEndTimes();


                    WritePluginConsole("Voting system enabled?: " + m_boolVotingSystemEnabled.ToString(), "Info", 5);
                    WritePluginConsole("Current player count is: " + csiServerInfo.PlayerCount, "Info", 5);
                    WritePluginConsole("Current game mode is: " + csiServerInfo.GameMode, "Info", 5);
                    WritePluginConsole("Current round time is: " + csiServerInfo.RoundTime, "Info", 5);
                    WritePluginConsole("Current number of teams is: " + csiServerInfo.TeamScores.Count, "Info", 5);
                    //if (csiServerInfo.TeamScores.Count >= 2)
                    //{
                    foreach (TeamScore ts in csiServerInfo.TeamScores)
                    {
                        WritePluginConsole("Team " + ts.TeamID + " score: " + ts.Score, "Info", 5);
                        WritePluginConsole("Team " + ts.TeamID + " winningscore: " + ts.WinningScore, "Info", 5);
                    }

                    //WritePluginConsole("Team 0 score: " + csiServerInfo.TeamScores[0].Score, "Info", 5);
                    //WritePluginConsole("Team 1 score: " + csiServerInfo.TeamScores[1].Score, "Info", 5);
                    //WritePluginConsole("Team 0 winningscore: " + csiServerInfo.TeamScores[0].WinningScore, "Info", 5);
                    //WritePluginConsole("Team 1 winningscore: " + csiServerInfo.TeamScores[1].WinningScore, "Info", 5);
                    //}

                    this.ExecuteCommand("procon.protected.send", "mapList.list");
                    this.ExecuteCommand("procon.protected.send", "mapList.getMapIndices");

                    this.m_timeServerInfoCheck = DateTime.Now.AddSeconds(20);

                    WritePluginConsole("ServerInfo updated!", "Info", 5);
                }
                else if (this.m_timeServerInfoCheck >= DateTime.Now)
                {
                    WritePluginConsole("ServerInfo called too soon, skipping this call.", "Info", 4);
                    WritePluginConsole(String.Format("Now: {0:G}", DateTime.Now), "Info", 5);
                    WritePluginConsole(String.Format("Next: {0:G}", m_timeServerInfoCheck), "Info", 5);
                }
                else
                {
                    WritePluginConsole("Skipping ServerInfo call because voting system is disabled.", "Info", 4);
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in: OnServerInfo", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }

            UpdateCheck();
        }

        private void UpdateCheck()
        {
            if (Check4Update != enumBoolYesNo.Yes)
            {
                return;
            }

            DateTime updatehelper = lastupdatecheck.AddHours(3);
            if (DateTime.Compare(updatehelper, DateTime.Now) > 0){
                return;
            }

            String latestVersion = "Unknown - Request failed!";

            try
            {
                WebClient client = new WebClient();
                String response = client.DownloadString("https://gitlab.com/e4gl/xVotemap/-/raw/master/version.json");
                Hashtable json = (Hashtable)JSON.JsonDecode(response);


                if (json == null)
                {
                    this.ExecuteCommand("procon.protected.pluginconsole.write",
                                        "Update check failed - gitlab.com/e4gl/xVotemap is private! Please contact the maintainer!");

                    return;
                }

                if (json.ContainsKey("latest_version"))
                {
                    latestVersion = (String)json["latest_version"];
                }
                else
                {
                    this.ExecuteCommand("procon.protected.pluginconsole.write",
                                        "Update check failed - Cannot extract latest version! Please contact the maintainer!");
                    return;
                }
            }
            catch (Exception e)
            {
                this.ExecuteCommand("procon.protected.pluginconsole.write",
                                    "Unable to check for plugin updates!");
                this.ExecuteCommand("procon.protected.pluginconsole.write",
                                    "Make sure that the plugin does not run in sandbox mode!");
                return;
            }


            if (GetPluginVersion().CompareTo(GetPluginVersion()) != 0)
            {
                this.ExecuteCommand("procon.protected.pluginconsole.write", "xVotemap: ^b^2UPDATE " + latestVersion + " AVAILABLE");
                this.ExecuteCommand("procon.protected.pluginconsole.write", "xVotemap: your version: " + GetPluginVersion());
                this.ExecuteCommand("procon.protected.pluginconsole.write", "xVotemap: latest version " + latestVersion);
            }
            lastupdatecheck = DateTime.Now;
        }

        public void OnGameModeCounter(int limit)
        {
            WritePluginConsole("Setting ticketCounter to " + limit, "Info", 5);
            this.ticketCounter = limit;
        }

        public void OnMaplistList(List<MaplistEntry> lstMaplist)
        {
            try
            {
                if (!ListsEqual(lstMaplist, m_listCurrMapList) && m_listCurrMapList.Count > 0)
                {
                    WritePluginConsole("Maplist change detected", "Info", 2);
                    if (m_boolVotingStarted)
                    {
                        WritePluginConsole("Restarting vote due to maplist change", "Work", 1);
                        // StartVotingPoll();
                    }
                }
            }
            catch (Exception e)
            {
                WritePluginConsole("Caught Exception in ListsEqual", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
                throw;
            }
            this.m_listCurrMapList = new List<MaplistEntry>(lstMaplist);
            WritePluginConsole("Maplist updated. There are " + m_listCurrMapList.Count + " maps currently in the maplist", "Info", 5);
        }

        public void OnMaplistGetMapIndices(int mapIndex, int nextIndex)
        {
            this.m_iCurrMapIndex = mapIndex;
            this.m_iNextMapIndex = nextIndex;

            if (this.m_strNextMap == "")
            {
                this.m_strNextMap = GetMapByFilename(m_listCurrMapList[nextIndex].MapFileName).PublicLevelName;
                this.m_strNextMode = ConvertGamemodeToShorthand(m_listCurrMapList[nextIndex].Gamemode);
            }
        }

        public void OnGlobalChat(string speaker, string message)
        {
            ProcessChatMessage(speaker, message);
        }

        public void OnTeamChat(string speaker, string message, int teamId)
        {
            ProcessChatMessage(speaker, message);
        }

        public void OnSquadChat(string speaker, string message, int teamId, int squadId)
        {
            ProcessChatMessage(speaker, message);
        }

        public void OnRoundOver(int winningTeamId)
        {
            WritePluginConsole("Round Over: Stopping voting system", "Work", 3);
            if (m_boolVotingStarted && m_boolVotingSystemEnabled)
            {
                StopVotingSystem();
                DisplayVoteResults();
            }
            else
            {
                StopVotingSystem();
            }

        }

        public override void OnLoadingLevel(string mapFileName, int roundsPlayed, int roundsTotal) {
            CMap map = GetMapByFilename(mapFileName);
            OnLevelLoaded(mapFileName, map.GameMode, roundsPlayed, roundsTotal);
        }

        public void OnLevelLoaded(string mapFileName, string Gamemode, int roundsPlayed, int roundsTotal)
        {
            //this.ExecuteCommand("procon.protected.pluginconsole.write", mapFileName + Gamemode + roundsPlayed.ToString() + roundsTotal.ToString());
            WritePluginConsole("Level loaded: ^6" + GetMapByFilename(mapFileName).PublicLevelName + " " + ConvertGamemodeToShorthand(Gamemode) + "^0, Round " + (roundsPlayed + 1).ToString() + " of " + roundsTotal.ToString(), "Info", 3);
            if (roundsPlayed + 1 == roundsTotal || roundsTotal == 0)
            {
                this.m_boolOnLastRound = true;
                WritePluginConsole("Level loaded: Last round detected, starting voting system", "Work", 3);
                StartVotingSystem();
            }
            else
            {
                this.m_boolOnLastRound = false;
                WritePluginConsole("Level loaded: Not last round, voting system will not be enabled", "Work", 3);
            }

            for (int i = 0; i < m_listPastMaps.Count; i++)
            {
                if (m_listPastMaps[i].MapFileName == mapFileName && m_listPastMaps[i].Gamemode == Gamemode)
                {
                    WritePluginConsole("Map and mode found in played maplist. Removing.", "Work", 5);
                    m_listPastMaps.RemoveAt(i);
                    break;
                }
            }
            m_listPastMaps.Add(new MaplistEntry(Gamemode, mapFileName, roundsTotal));

        }

        public void OnRestartLevel()
        {
            WritePluginConsole("Level Restarted: Stopping voting system", "Work", 3);
            StopVotingSystem();
        }

        public void OnRunNextLevel()
        {
            WritePluginConsole("Skipped Level: Stopping voting system", "Work", 3);
            if (m_boolVotingStarted && m_boolVotingSystemEnabled)
            {
                StopVotingSystem();
                DisplayVoteResults();
            }
            else
            {
                StopVotingSystem();
            }
        }

        #endregion

        #region Votemap

        public virtual void OnRoundOverTeamScores(List<TeamScore> teamScores)
        {
            if (m_boolOnLastRound)
            {
                DisplayNextMap("all");
            }
        }

        public void RunVotingLogic()
        {
            WritePluginConsole("Running Voting Logic", "Info", 5);

            if (m_boolVotemapEnabled && showNextMapBeforeVote == enumBoolYesNo.Yes && !this.nextmapShown && m_strTrigger == "Automatic" && m_boolOnLastRound && !m_boolVotingStarted && m_boolVotingSystemEnabled && DateTime.Now > m_timeVoteStart.AddMinutes(-1) && DateTime.Now < m_timeVoteEnd && m_iCurrPlayerCount >= minimumPlayers && m_iCurrPlayerCount <= maximumPlayers)
            {
                DisplayNextMap("all");
                this.nextmapShown = true;
            }

            if (m_boolVotemapEnabled && m_strTrigger == "Automatic" && m_boolOnLastRound && !m_boolVotingStarted && m_boolVotingSystemEnabled && DateTime.Now > m_timeVoteStart && DateTime.Now < m_timeVoteEnd && m_iCurrPlayerCount >= minimumPlayers && m_iCurrPlayerCount <= maximumPlayers)
            {
                StartVotingPoll();
            }
            else if (m_boolVotemapEnabled && m_boolVotingStarted && m_boolVotingSystemEnabled && (DateTime.Now > m_timeVoteEnd || DateTime.Now > m_timeVoteStart.AddSeconds(m_iVotingDuration)))
            {
                StopVotingSystem();
                DisplayVoteResults();
            }
        }

        private void StartVotingSystem()
        {
            m_boolVotingStarted = false;
            m_boolVotingSystemEnabled = true;

            m_timeVoteStart = DateTime.Now.AddHours(1);
            m_timeVoteEnd = DateTime.Now.AddHours(1);
            m_timePrevious = DateTime.Now;
            this.nextmapShown = false;
            this.m_iPrevTicket = null;
            this.currentMcom = 0;
            this.ticketCounter = 100;
            //  this.m_iPrevTicket[0] = -1;
            this.m_strNextMap = "";

            this.m_dictVoting = new Dictionary<string, int>();
            this.m_players = new Players();

            this.ExecuteCommand("procon.protected.send", "vars.gameModeCounter");

            this.ExecuteCommand("procon.protected.tasks.add", "taskRunVotingLogic", "0", "5", "-1", "procon.protected.plugins.call", "xVotemap", "RunVotingLogic");
            

            myvotes = new Dictionary<string, int>();
            //myvipvotes = new Dictionary<string, int>();
        }

        private void StopVotingSystem()
        {
            m_boolVotingStarted = false;
            m_boolVotingSystemEnabled = false;

            m_timePrevious = DateTime.Now;
            this.m_iPrevTicket = null;
            //  this.m_iPrevTicket[0] = -1;

            this.ExecuteCommand("procon.protected.tasks.remove", "taskDisplayVoteOptions");
            this.ExecuteCommand("procon.protected.tasks.remove", "taskRunVotingLogic");
            this.ExecuteCommand("procon.protected.tasks.remove", "taskDisplayNextMap");
            this.ExecuteCommand("procon.protected.tasks.remove", "taskDisplayVoteBanner");
            this.ExecuteCommand("procon.protected.plugins.call", "CUltimateMapManager", "MapVoteStopped");
        }

        private void SetVoteStartAndEndTimes()
        {
            try
            {
                int winningScore = 0;
                if (m_strCurrentGameMode.ToLower().Contains("conquest") || m_strCurrentGameMode.Contains("DeathMatch") || m_strCurrentGameMode.Contains("Domination") || m_strCurrentGameMode.Contains("Superiority") || m_strCurrentGameMode.Contains("Scavenger") || m_strCurrentGameMode.Contains("Obliteration") || m_strCurrentGameMode.Contains("Chainlink0") || m_strCurrentGameMode.Contains("SQDM"))
                {


                    if (m_strCurrentGameMode.Contains("TeamDeathMatch"))
                    {
                        winningScore = ticketCounter;
                    }
                    else if (m_strCurrentGameMode.Contains("SquadDeathMatch"))
                    {
                        winningScore = ticketCounter / 2;
                    }


                    int timeToEnd = 9999;
                    if (m_iPrevTicket != null)
                    {

                        //WritePluginConsole("i", "Info", 5);

                        if (m_iPrevTicket[0] != -1)
                        {

                            //WritePluginConsole("am", "Info", 5);


                            TimeSpan tspan = DateTime.Now - m_timePrevious;

                            //WritePluginConsole("a", "Info", 5);


                            double[] timeRemaining = new double[m_listCurrTeamScore.Count];
                            //double[] timeRemaining = new double[numteams];


                            for (int i = 0; i < m_listCurrTeamScore.Count; i++)
                            //for (int i = 0; i < numteams; i++)
                            {
                                //WritePluginConsole("little", "Info", 5);
                                WritePluginConsole("Ticket Count: Team " + this.m_listCurrTeamScore[i].TeamID.ToString() + ": " + this.m_listCurrTeamScore[i].Score + "/" + this.m_listCurrTeamScore[i].WinningScore.ToString(), "Info", 5);

                                //int score;
                                //try
                                //{
                                //    score = m_listCurrTeamScore[i].Score;
                                //}
                                //catch
                                //{
                                //    score = 0;
                                //}

                                double iTicketDiff = (double)(m_listCurrTeamScore[i].Score - m_iPrevTicket[i]);
                                //double iTicketDiff = (double)(score - m_iPrevTicket[i]);
                                double dbTicketPerSec = iTicketDiff / tspan.TotalSeconds;

                                timeRemaining[i] = 9999;

                                if (m_strCurrentGameMode.Contains("Obliteration"))
                                {
                                    if (m_listCurrTeamScore[i].Score == 1)
                                    {
                                        timeRemaining[i] = 5;
                                    }
                                }
                                else if (dbTicketPerSec != 0)
                                {
                                    timeRemaining[i] = Convert.ToDouble(winningScore - m_listCurrTeamScore[i].Score) / dbTicketPerSec - m_iStopVoteTime;
                                }
                            }

                            timeToEnd = Convert.ToInt32(GetMin(timeRemaining));

                            if (m_iCurrentRoundTime + m_iStopVoteTime + m_iVotingDuration >= 3300)
                            {
                                timeToEnd = 5;
                            }
                            else if (timeToEnd > 3300 - (m_iCurrentRoundTime + m_iStopVoteTime + m_iVotingDuration))
                            {
                                timeToEnd = 3300 - (m_iCurrentRoundTime + m_iStopVoteTime + m_iVotingDuration);
                            }

                            //timeToEnd = 100;

                            //try
                            //{
                            //WritePluginConsole("Ticket Count: Team " + this.m_listCurrTeamScore[0].TeamID.ToString() + ": " + this.m_listCurrTeamScore[0].Score + "/" + this.m_listCurrTeamScore[0].WinningScore.ToString(), "Info", 5);
                            //}
                            //catch
                            //{
                            //    WritePluginConsole("TeamScores not ok. Not showing Ticket Count.", "Error", 3);
                            //}

                            if (m_boolVotingSystemEnabled && !m_boolVotingStarted)
                            {

                                int iSecondsTillVoteStart = timeToEnd - this.m_iVotingDuration;
                                m_timeVoteStart = DateTime.Now.AddSeconds(iSecondsTillVoteStart);

                                if (iSecondsTillVoteStart > 0)
                                {
                                    m_tsStartVote = TimeSpan.FromSeconds(iSecondsTillVoteStart);
                                    if (m_strCurrentGameMode.Contains("Obliteration"))
                                    {
                                        WritePluginConsole("Voting starts when any of the Teams has 1 Bomb Site left", "Info", 3);
                                    }
                                    else
                                    {
                                        WritePluginConsole("Estimated voting start in " + ToReadableString(m_tsStartVote) + " @ " + String.Format("{0:T}", this.m_timeVoteStart), "Info", 3);
                                    }
                                }
                                else
                                {
                                    WritePluginConsole("Estimated voting start " + (-iSecondsTillVoteStart).ToString() + " seconds ago, @ " + String.Format("{0:T}", this.m_timeVoteStart), "Info", 3);
                                }
                            }

                            if (m_boolVotingSystemEnabled)
                            {
                                m_timeVoteEnd = DateTime.Now.AddSeconds(timeToEnd + this.m_iStopVoteTime - this.m_iEndTimeLeeway);
                            }

                            if (m_boolVotingStarted)
                            {
                                TimeSpan tempSpan = DateTime.Now - this.m_timeVoteStart;
                                WritePluginConsole("Voting duration: " + ToReadableString(tempSpan) + ".", "Info", 3);
                            }


                            m_timePrevious = DateTime.Now;
                        }

                        //try
                        //{
                        for (int i = 0; i < m_iPrevTicket.Length; i++)
                        {
                            //WritePluginConsole("piggy", "Info", 5);
                            m_iPrevTicket[i] = m_listCurrTeamScore[i].Score;
                        }
                        //}
                        //catch
                        //{
                        //    WritePluginConsole("TeamScores not ok. Not setting Scores.", "Error", 3);
                        //}
                    }
                }
                else if (m_strCurrentGameMode.ToLower().Contains("rush") || m_strCurrentGameMode.Contains("GunMaster") || m_strCurrentGameMode.Contains("Capture") || m_strCurrentGameMode.Contains("Elimination"))
                {

                    //if (m_iPrevTicket != null && m_iPrevTicket[0] != -1 && m_listCurrTeamScore != null && m_listCurrTeamScore.Count > 0)
                    //{
                    //    if (maxTickets < m_listCurrTeamScore[0].Score)
                    //    {
                    //        maxTickets = m_listCurrTeamScore[0].Score;

                    //        if (m_listCurrTeamScore[0].Score > m_iPrevTicket[0] + 5 && m_listCurrTeamScore[0].Score + 10 > maxTickets)
                    //        {
                    //            currentMcom++;
                    //        }

                    //    }
                    //    WritePluginConsole("Current Mcom is: " + currentMcom.ToString() + ". Ticket count: " + m_listCurrTeamScore[0].Score, "Info", 2);
                    //}



                    //m_timePrevious = DateTime.Now;
                    //m_iPrevTicket[0] = m_listCurrTeamScore[0].Score;

                    if (m_boolVotingSystemEnabled && !m_boolVotingStarted)
                    {
                        m_timeVoteStart = DateTime.Now.AddSeconds(m_iRushVoteStartTime - m_iCurrentRoundTime);
                        m_timeVoteEnd = m_timeVoteStart.AddSeconds(m_iVotingDuration);

                        if (DateTime.Now < m_timeVoteStart)
                        {
                            WritePluginConsole("Voting will start in " + ToReadableString(this.m_timeVoteStart - DateTime.Now) + " @ " + String.Format("{0:T}", this.m_timeVoteStart), "Info", 3);
                        }
                        else
                        {
                            WritePluginConsole("Voting should have started " + ToReadableString(DateTime.Now - this.m_timeVoteStart) + " ago, @ " + String.Format("{0:T}", this.m_timeVoteStart), "Info", 3);
                        }
                    }

                }
                else if (m_strCurrentGameMode.Contains("CarrierAssault"))
                {
                    if (m_boolVotingSystemEnabled && !m_boolVotingStarted)
                    {
                        m_timeVoteStart = DateTime.Now.AddSeconds(m_iCalVoteStartTime - m_iCurrentRoundTime);
                        m_timeVoteEnd = m_timeVoteStart.AddSeconds(m_iVotingDuration);

                        if (DateTime.Now < m_timeVoteStart)
                        {
                            WritePluginConsole("Voting will start in " + ToReadableString(this.m_timeVoteStart - DateTime.Now) + " @ " + String.Format("{0:T}", this.m_timeVoteStart), "Info", 3);
                        }
                        else
                        {
                            WritePluginConsole("Voting should have started " + ToReadableString(DateTime.Now - this.m_timeVoteStart) + " ago, @ " + String.Format("{0:T}", this.m_timeVoteStart), "Info", 3);
                        }
                    }
                }
                else
                {
                    WritePluginConsole("Current gamemode is not recognised: " + m_strCurrentGameMode, "Info", 1);
                }


            }
            catch (Exception e)
            {
                WritePluginConsole("EXCEPTION CAUGHT IN: SetVoteStartAndEndTimes", "Error", 1);
                WritePluginConsole(e.ToString(), "Error", 1);
            }
        }

        private void ProcessChatMessage(string speaker, string message)
        {
            CPrivileges cpPlayerPrivs = this.GetAccountPrivileges(speaker);

            // --------------
            // VOTE
            // --------------
            Match match = Regex.Match(message, @"" + m_strHosVotePrefix + @"(\d)");
            if (match.Success && message.Length == (m_strHosVotePrefix.Length + 1))
            {
                if (m_boolVotingStarted && !m_dictVoting.ContainsKey(speaker))
                {
                    int vote = Convert.ToInt32(match.Groups[m_strHosVotePrefix.Length].Value);

                    if (vote > 0 && vote <= m_listMapOptions.Count)
                    {
                        // addon: handler for spectators
                        if (m_enumAllowSpectatorsToVote == enumBoolYesNo.No)
                        {
                            Players.Player curPlayer = m_players.GetPlayer(speaker);
                            if (curPlayer == null)
                            {
                                WritePluginConsole(speaker + " was not found in player list. Spectator check failed!.", "Error", 1);
                            }
                            else
                            {
                                if (curPlayer.Type == (int)PlayerType.Spectator)
                                {
                                    WritePluginConsole(speaker + " is a spectator and not allowed to vote!", "Spectator", 2);
                                    this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": Spectators are not allowed to vote!", "player", speaker);
                                    return;
                                }
                            }
                        }
                        // addon end

                        m_dictVoting.Add(speaker, vote - 1);
                        bool success = m_players.SetVote(speaker, vote - 1);

                        if (!success)
                        {
                            WritePluginConsole(speaker + " was not found in player list. Vote not counted.", "Error", 1);
                        }
                        else
                        {
                            if (!myvotes.ContainsKey(speaker))
                            {
                                myvotes.Add(speaker, vote);
                            }
                        }

                        CMap map = GetMapByFilename(m_listMapOptions[m_dictVoting[speaker]]);
                        string mapandmode = map.PublicLevelName;
                        if (m_enumShowGamemode == enumBoolYesNo.Yes)
                        {
                            mapandmode += " " + ConvertGamemodeToShorthand(map.GameMode);
                        }

                        if (this.confirmpub == enumBoolYesNo.Yes)
                        {
                            string msg = this.pubconfirmmsg;
                            msg = msg.Replace("%pn%", speaker);
                            msg = msg.Replace("%map%", mapandmode);
                            this.ExecuteCommand("procon.protected.send", "admin.say", msg, "all");
                        }
                        else
                        {
                            this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You voted for " + mapandmode, "player", speaker);
                        }
                        WritePluginConsole("^7" + speaker + "^0 voted for ^6" + map.PublicLevelName + " " + ConvertGamemodeToShorthand(map.GameMode), "Info", 2);
                    }
                    else
                    {
                        this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": Your vote was not recognised. Please try again.", "player", speaker);
                    }
                }
                else if (m_boolVotingStarted && m_dictVoting.ContainsKey(speaker))
                {
                    int vote = Convert.ToInt32(match.Groups[m_strHosVotePrefix.Length].Value);
                    if (vote > 0 && vote <= m_listMapOptions.Count)
                    {
                        if (m_dictVoting[speaker] != vote - 1) {
                            CMap map0 = GetMapByFilename(m_listMapOptions[m_dictVoting[speaker]]);
                            CMap map1 = GetMapByFilename(m_listMapOptions[vote - 1]);
                            string mapandmode0 = map0.PublicLevelName;
                            string mapandmode1 = map1.PublicLevelName;
                            if (m_enumShowGamemode == enumBoolYesNo.Yes)
                            {
                                mapandmode0 += " " + ConvertGamemodeToShorthand(map0.GameMode);
                                mapandmode1 += " " + ConvertGamemodeToShorthand(map1.GameMode);
                            }

                            this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You changed your vote from " + mapandmode0 + " to " + mapandmode1, "player", speaker);
                            WritePluginConsole("^7" + speaker + "^0 changed their vote from ^6" + map0.PublicLevelName + " " + ConvertGamemodeToShorthand(map0.GameMode) + "^0 to ^6" + map1.PublicLevelName + " " + ConvertGamemodeToShorthand(map1.GameMode), "Info", 2);

                            m_dictVoting[speaker] = vote - 1;

                            myvotes.Remove(speaker);
                            //myvipvotes.Remove(speaker);

                            if (!myvotes.ContainsKey(speaker))
                            {
                                myvotes.Add(speaker, vote);
                            }
                        }
                        else {
                            CMap map = GetMapByFilename(m_listMapOptions[m_dictVoting[speaker]]);
                            string mapandmode = map.PublicLevelName;
                            if (m_enumShowGamemode == enumBoolYesNo.Yes)
                            {
                                mapandmode += " " + ConvertGamemodeToShorthand(map.GameMode);
                            }
                            this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You have already voted for " + mapandmode, "player", speaker);
                        }
                    }
                    else
                    {
                        this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": Your vote was not recognised. Please try again.", "player", speaker);
                    }
                }
                else if (m_boolVotingSystemEnabled)
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": There is no vote currently in progress. Voting will start in about " + ToReadableString(m_tsStartVote), "player", speaker);
                }
                else
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": Voting has ended, the next map will be " + m_strNextMap, "player", speaker);
                }
            }

            // --------------
            // VOTING OPTIONS
            // --------------
            match = Regex.Match(message, @"^[!/]v");
            if (match.Success && message.Length == 2)
            {
                if (m_boolVotingStarted)
                {
                    DisplayVoteOptions(speaker);
                    WritePluginConsole(speaker + " requested the map options", "Info", 3);
                }
                else if (m_boolVotingSystemEnabled)
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": There is no vote currently in progress. Voting will start in about " + ToReadableString(m_tsStartVote), "player", speaker);
                }
                else
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": Voting has ended, the next map will be " + m_strNextMap, "player", speaker);
                }
            }

            // --------------
            // NEXT MAP
            // --------------
            match = Regex.Match(message, @"^[!/]next ?map");
            if (match.Success)
            {
                WritePluginConsole(speaker + " requested the next map", "Info", 3);
                DisplayNextMap(speaker);
            }

            // --------------
            // CALL VOTEMAP
            // --------------
            match = Regex.Match(message, @"^[!/]vote ?map");
            if (match.Success && cpPlayerPrivs.CanUseMapFunctions)
            {
                WritePluginConsole("^7" + speaker + "^0 started a Votemap", "Info", 3);
                if (!m_boolVotingSystemEnabled)
                {
                    StartVotingSystem();
                }
                StartVotingPoll();
            }
            else if (match.Success)
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You do not have sufficient permissions to call a Votemap.", "player", speaker);
            }

            // --------------
            // ENABLE/DISABLE VOTE MAP
            // --------------
            match = Regex.Match(message, @"enablevotemap");
            if (match.Success && cpPlayerPrivs.CanUseMapFunctions)
            {
                WritePluginConsole("^7" + speaker + "^0 enabled xVotemap", "Info", 2);
                this.ExecuteCommand("procon.protected.send", "admin.say", "xVotemap ENABLED!", "player", speaker);

                this.m_boolVotemapEnabled = true;
            }
            else if (match.Success)
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You do not have sufficient permissions to enable xVotemap.", "player", speaker);
            }

            match = Regex.Match(message, @"disablevotemap");
            if (match.Success && cpPlayerPrivs.CanUseMapFunctions)
            {
                WritePluginConsole("^7" + speaker + "^0 diasbled xVotemap", "Info", 2);
                this.ExecuteCommand("procon.protected.send", "admin.say", "xVotemap DISABLED!", "player", speaker);

                this.m_boolVotemapEnabled = false;
            }
            else if (match.Success)
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": You do not have sufficient permissions to disable xVotemap.", "player", speaker);
            }

            // --------------
            // INCORRECT VOTE ATTEMPTS
            // --------------
            match = Regex.Match(message, @"^\d");
            if (match.Success && message.Length == 1 && m_boolVotingStarted && !m_dictVoting.ContainsKey(speaker))
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", speaker + ": If you are trying to vote, you seem to have forgotten the / in front of the map number.", "player", speaker);
            }
        }

        private void StartVotingPoll()
        {
            try
            {
                WritePluginConsole("Voting poll: Starting...", "Info", 4);
                m_boolVotingStarted = true;
                m_timeVoteStart = DateTime.Now;
                m_dictVoting = new Dictionary<string, int>();
                myvotes = new Dictionary<string, int>();

                GenerateMapOptions();

                if (m_boolVotingStarted)
                {
                    if (m_strEnableVoteBanner == "First")
                    {
                        DisplayVoteBanner();
                    }
                    else if (m_strEnableVoteBanner == "All")
                    {
                        this.ExecuteCommand("procon.protected.tasks.add", "taskDisplayVoteBanner", "0", m_iVotingOptionsInterval.ToString(), "-1", "procon.protected.plugins.call", "xVotemap", "DisplayVoteBanner");
                    }

                    GenerateVoteOptionsDisplay();

                    this.ExecuteCommand("procon.protected.tasks.add", "taskDisplayVoteOptions", "6", m_iVotingOptionsInterval.ToString(), "-1", "procon.protected.plugins.call", "xVotemap", "DisplayVoteOptions", "all");
                }
                // inform UMM about the running vote
                this.ExecuteCommand("procon.protected.plugins.call", "CUltimateMapManager", "MapVoteStarted");
                WritePluginConsole("^bVoting poll: Started!", "Info", 3);
            }
            catch (Exception e)
            {
                WritePluginConsole("EXCEPTION CAUGHT IN: StartVotingPoll", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        public void DisplayVoteBanner()
        {
            List<Players.Squad> squads = m_players.GetNonvotedSquads();
            string temp = "";
            foreach (Players.Squad s in squads)
            {
                temp += "(" + s.TeamID + "," + s.SquadID + ") ";
            }
            WritePluginConsole("Nonvoted squads:" + temp, "Info", 4);

            if (this.m_strBannerType == "Yell" || this.m_strBannerType == "Both")
            {
                this.ExecuteCommand("procon.protected.send", "admin.yell", YellVoteBanner, m_iBannerYellDuration.ToString(), "all");
            }

            if (this.m_strBannerType == "Chat" || this.m_strBannerType == "Both")
            {
                foreach (string msg in VoteBanner)
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", msg, "all");
                }
            }

            WritePluginConsole("Displayed vote banner", "Work", 3);
        }

        private void GenerateMapOptions()
        {
            try
            {
                WritePluginConsole("Generate map options: Starting...", "Info", 4);
                m_listMapOptions = new List<string>();
                m_listGamemodeOptions = new List<string>();
                List<MaplistEntry> tempMaplist = new List<MaplistEntry>(m_listCurrMapList);
                string mapnames = "";

                try
                {
                    for (int i = tempMaplist.Count - 1; i > -1; i--)
                    {
                        if (tempMaplist[i].MapFileName == m_strCurrentMap && m_enumExcludeCurrMap == enumBoolYesNo.Yes)
                        {
                            WritePluginConsole("^6" + GetMapByFilename(tempMaplist[i].MapFileName).PublicLevelName + " " + ConvertGamemodeToShorthand(tempMaplist[i].Gamemode) + "^0 was removed from maplistoptions.", "Info", 3);
                            tempMaplist.RemoveAt(i);
                            //if (!mapstoremove.Contains(i))
                            //    mapstoremove.Add(i);
                        }
                        else if (tempMaplist[i].Gamemode == m_strCurrentGameMode && m_enumExcludeCurrMode == enumBoolYesNo.Yes)
                        {
                            WritePluginConsole("^6" + GetMapByFilename(tempMaplist[i].MapFileName).PublicLevelName + " " + ConvertGamemodeToShorthand(tempMaplist[i].Gamemode) + "^0 was removed from maplistoptions.", "Info", 3);
                            tempMaplist.RemoveAt(i);
                            //if (!mapstoremove.Contains(i))
                            //    mapstoremove.Add(i);
                        }
                    }
                }
                catch (Exception e)
                {
                    WritePluginConsole("Exception Caught while trying to remove current map, continuing without removing =\\", "Error", 1);
                    WritePluginConsole(e.Message, "Error", 1);
                }

                try
                {
                    for (int i = tempMaplist.Count - 1; i > -1; i--)
                    {
                        for (int j = i - 1; j > -1; j--)
                        {
                            if (tempMaplist[i].MapFileName == tempMaplist[j].MapFileName && tempMaplist[i].Gamemode == tempMaplist[j].Gamemode)
                            {
                                WritePluginConsole(tempMaplist[i].MapFileName + " equals " + tempMaplist[j].MapFileName + " and " + tempMaplist[i].Gamemode + " equals " + tempMaplist[j].Gamemode + " removing duplicate", "Info", 3);
                                tempMaplist.RemoveAt(j);
                                //if (!mapstoremove.Contains(j))
                                //    mapstoremove.Add(j);
                                break;
                            }
                        }
                    }
                    //foreach (int index in mapstoremove)
                    //{
                    //    tempMaplist.RemoveAt(index);
                    //}    
                }
                catch (Exception e)
                {
                    WritePluginConsole("Exception Caught while trying to remove duplicate map, continuing without removing =\\", "Error", 1);
                    WritePluginConsole(e.Message, "Error", 1);
                }

                ////List<int> mapstoremove = new List<int>();

                //try
                //{
                //    for (int i = 0; i < tempMaplist.Count; i++)
                //    {
                //        if ((tempMaplist[i].MapFileName == m_strCurrentMap && tempMaplist[i].Gamemode == m_strCurrentGameMode) && m_enumExcludeCurrMap == enumBoolYesNo.Yes)
                //        {
                //            WritePluginConsole("^6" + GetMapByFilename(tempMaplist[i].MapFileName).PublicLevelName + " " + ConvertGamemodeToShorthand(tempMaplist[i].Gamemode) + "^0 was removed from maplistoptions.", "Info", 3);
                //            tempMaplist.RemoveAt(i);
                //            //if (!mapstoremove.Contains(i))
                //            //    mapstoremove.Add(i);
                //        }
                //    }
                //}
                //catch (Exception e)
                //{
                //    WritePluginConsole("Exception Caught while trying to remove current map, continuing without removing =\\", "Error", 1);
                //    WritePluginConsole(e.Message, "Error", 1);
                //}

                //try
                //{
                //    for (int i = 0; i < tempMaplist.Count; i++)
                //    {
                //        for (int j = i + 1; j < tempMaplist.Count; j++)
                //        {
                //            if (tempMaplist[i].MapFileName == tempMaplist[j].MapFileName && tempMaplist[i].Gamemode == tempMaplist[j].Gamemode)
                //            {
                //                WritePluginConsole(tempMaplist[i].MapFileName + " equals " + tempMaplist[j].MapFileName + " and " + tempMaplist[i].Gamemode + " equals " + tempMaplist[j].Gamemode + " removing duplicate", "Info", 3);
                //                tempMaplist.RemoveAt(j);
                //                //if (!mapstoremove.Contains(j))
                //                //    mapstoremove.Add(j);
                //            }
                //        }
                //    }
                //    //foreach (int index in mapstoremove)
                //    //{
                //    //    tempMaplist.RemoveAt(index);
                //    //}    
                //}
                //catch (Exception e)
                //{
                //    WritePluginConsole("Exception Caught while trying to remove duplicate map, continuing without removing =\\", "Error", 1);
                //    WritePluginConsole(e.Message, "Error", 1);
                //}                

                tempMaplist = SortMapList(tempMaplist);


                int iNumOfOptions = Math.Min(m_iNumOfMapOptions, tempMaplist.Count);

                if (iNumOfOptions > 1)
                {
                    List<int> selection = new List<int>();
                    try
                    {
                        selection = PseudoRandom(iNumOfOptions, tempMaplist.Count, m_iRandomness);
                    }
                    catch (Exception e)
                    {
                        WritePluginConsole("EXCEPTION CAUGHT IN: PseudoRandom", "Error", 1);
                        WritePluginConsole(e.Message, "Error", 1);
                        throw;
                    }

                    // sort by map name length
                    selection.Sort(delegate (int map1, int map2)
                    {
                        return GetMapByFilename(tempMaplist[map2].MapFileName).PublicLevelName.Length.CompareTo(GetMapByFilename(tempMaplist[map1].MapFileName).PublicLevelName.Length);
                    });

                    // put longest names with shortest
                    List<int> newSelection = new List<int>();
                    int k = 0;
                    while (selection.Count > 0)
                    {
                        if (k % 2 == 0)
                        {
                            newSelection.Add(selection[0]);
                            selection.RemoveAt(0);
                        }
                        else
                        {
                            newSelection.Add(selection[selection.Count - 1]);
                            selection.RemoveAt(selection.Count - 1);
                        }
                        k++;
                    }

                    for (int i = 0; i < newSelection.Count; i++)
                    {
                        m_listMapOptions.Add(tempMaplist[newSelection[i]].MapFileName);
                        m_listGamemodeOptions.Add(tempMaplist[newSelection[i]].Gamemode);

                        mapnames += "^6" + GetMapByFilename(m_listMapOptions[i]).PublicLevelName + " " + ConvertGamemodeToShorthand(GetMapByFilename(m_listMapOptions[i]).GameMode) + "^0 | ";
                    }

                    WritePluginConsole("^bVotemap Options: " + mapnames.Substring(0, mapnames.Length - 3), "Info", 2);
                }
                else
                {
                    WritePluginConsole("Not enough maps in the maplist to start a vote. Stopping voting system", "Info", 2);
                    StopVotingSystem();
                }
                WritePluginConsole("GenerateMapOptions: Done!", "Info", 4);
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in GenerateMapOptions", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }
        }

        private List<MaplistEntry> SortMapList(List<MaplistEntry> origMaplist)
        {
            try
            {
                WritePluginConsole("SortMapList: Starting...", "Info", 4);
                WritePluginConsole("Pastmaplist count:" + m_listPastMaps.Count, "Info", 4);
                WritePluginConsole("Currmaplist count:" + origMaplist.Count, "Info", 4);
                for (int i = 0; i < m_listPastMaps.Count; i++)
                {
                    for (int j = 0; j < origMaplist.Count; j++)
                    {
                        if (mapSortSettings == "Map Only")
                        {
                            if (m_listPastMaps[i].MapFileName == origMaplist[j].MapFileName)
                            {
                                MaplistEntry tempMap = origMaplist[j];
                                origMaplist.RemoveAt(j);
                                origMaplist.Add(tempMap);
                                WritePluginConsole(GetMapByFilename(tempMap.MapFileName).PublicLevelName + " Added", "Info", 5);
                            }
                        }
                        else if (mapSortSettings == "Map and Gametype")
                        {
                            if (m_listPastMaps[i].MapFileName == origMaplist[j].MapFileName && m_listPastMaps[i].Gamemode == origMaplist[j].Gamemode)
                            {
                                MaplistEntry tempMap = origMaplist[j];
                                origMaplist.RemoveAt(j);
                                origMaplist.Add(tempMap);
                                WritePluginConsole(GetMapByFilename(tempMap.MapFileName).PublicLevelName + " Added", "Info", 5);
                                break;
                            }
                        }
                        else if (mapSortSettings == "Gametype Only")
                        {
                            if (m_listPastMaps[i].Gamemode == origMaplist[j].Gamemode)
                            {
                                MaplistEntry tempMap = origMaplist[j];
                                origMaplist.RemoveAt(j);
                                origMaplist.Add(tempMap);
                                WritePluginConsole(GetMapByFilename(tempMap.MapFileName).PublicLevelName + " Added", "Info", 5);
                            }
                        }
                    }
                }

                for (int j = 0; j < origMaplist.Count; j++)
                {
                    WritePluginConsole("Sorted: " + j.ToString() + " ^6" + GetMapByFilename(origMaplist[j].MapFileName).PublicLevelName + " " + ConvertGamemodeToShorthand(origMaplist[j].Gamemode), "Info", 4);
                }

                WritePluginConsole("SortMapList: Done!", "Info", 4);
            }
            catch (Exception e)
            {
                WritePluginConsole("Exception caught in SortMapList, continuing without proper map order =\\", "Error", 1);
                WritePluginConsole(e.Message, "Error", 1);
            }

            return origMaplist;
        }

        private void GenerateVoteOptionsDisplay()
        {
            lock (derplock)
            {
                m_listOptsDisplay = new List<string>();
                m_listOptsDisplay.Add("Type: " + m_strHosVotePrefix + "1, " + m_strHosVotePrefix + "2, ... in chat to vote for the next map!");

                string padding = "";
                string strNamenMode = "";
                string strLine = "";
                int iChatMiddle = 15;

                Dictionary<int, int> myvotescount = new Dictionary<int, int>();
                foreach (KeyValuePair<string, int> kvp in myvotes)
                {
                    if (!myvotescount.ContainsKey(kvp.Value))
                    {
                        myvotescount.Add(kvp.Value, 0);
                    }
                    myvotescount[kvp.Value]++;

                    if (vips.Contains(kvp.Key))
                    {
                        for (int i = 1; i < vipvotecount; i++)
                        {
                            myvotescount[kvp.Value]++;
                        }
                    }
                }

                for (int i = 0; i < m_listGamemodeOptions.Count; i++)
                {
                    if (!myvotescount.ContainsKey(i + 1))
                    {
                        myvotescount.Add(i + 1, 0);
                    }
                }

                for (int i = 0; i < m_listGamemodeOptions.Count; i++)
                {
                    if (i % 2 == 0)
                    {
                        if (m_enumShowGamemode == enumBoolYesNo.Yes)
                        {
                            strNamenMode = GetMapByFilename(m_listMapOptions[i]).PublicLevelName + " " + ConvertGamemodeToShorthand(GetMapByFilename(m_listMapOptions[i]).GameMode);
                            padding = GeneratePadding(strNamenMode, iChatMiddle);
                        }
                        else
                        {
                            strNamenMode = GetMapByFilename(m_listMapOptions[i]).PublicLevelName;
                            padding = GeneratePadding(GetMapByFilename(m_listMapOptions[i]).PublicLevelName, iChatMiddle);
                        }

                        if (displayvotescount == enumBoolYesNo.Yes)
                        {
                            strLine = " " + m_strHosVotePrefix + (i + 1).ToString() + " " + strNamenMode + " [" + myvotescount[i + 1] + "]" + " ";
                        }
                        else
                        {
                            strLine = " " + m_strHosVotePrefix + (i + 1).ToString() + " " + strNamenMode + " ";
                        }
                    }
                    else
                    {
                        if (m_enumShowGamemode == enumBoolYesNo.Yes)
                        {
                            strNamenMode = GetMapByFilename(m_listMapOptions[i]).PublicLevelName + " " + ConvertGamemodeToShorthand(GetMapByFilename(m_listMapOptions[i]).GameMode);
                        }
                        else
                        {
                            strNamenMode = GetMapByFilename(m_listMapOptions[i]).PublicLevelName;
                        }

                        if (displayvotescount == enumBoolYesNo.Yes)
                        {
                            strLine += "| " + m_strHosVotePrefix + (i + 1).ToString() + " " + strNamenMode + " [" + myvotescount[i + 1] + "]";
                        }
                        else
                        {
                            strLine += "| " + m_strHosVotePrefix + (i + 1).ToString() + " " + strNamenMode + "";
                        }

                        m_listOptsDisplay.Add(strLine);
                        strLine = "";
                    }

                    if (i == m_listGamemodeOptions.Count - 1 && i % 2 == 0)
                    {
                        m_listOptsDisplay.Add(strLine);
                        strLine = "";
                    }
                }
            }
        }

        public void DisplayVoteOptions(string target)
        {
            GenerateVoteOptionsDisplay();

            lock (derplock)
            {
                foreach (string s in m_listOptsDisplay)
                {
                    if (target == "all")
                    {
                        this.ExecuteCommand("procon.protected.send", "admin.say", s, "all");
                        WritePluginConsole(s, "Info", 5);
                    }
                    else
                    {
                        this.ExecuteCommand("procon.protected.send", "admin.say", s, "player", target);
                        WritePluginConsole(s, "Info", 5);
                    }
                }
            }
            WritePluginConsole("Displayed voting options", "Work", 4);
        }


        private void DisplayVoteResults()
        {
            // hmmm... well a separate function would cause redundancy
            Hashtable logData = new Hashtable{
                {"timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}
            };

            int[] votes = new int[m_listMapOptions.Count];
            int vipvotes = 0;

            foreach (KeyValuePair<string, int> kv in m_dictVoting)
            {
                votes[kv.Value]++;

                if (this.vips.Contains(kv.Key))
                {
                    for (int i = 1; i < vipvotecount; i++)
                    {
                        votes[kv.Value]++;
                        vipvotes++;
                    }
                }

            }

            int winner = MaxIndex(votes);
            double winningPercent = 0;
            if (m_dictVoting.Count != 0)
            {
                winningPercent = Convert.ToDouble(votes[winner]) / Convert.ToDouble(m_dictVoting.Count + vipvotes);
            }

            if (winningPercent > 1)
            {
                winningPercent = 1;
            }

            if (m_iCurrPlayerCount > 0)
            {
                double votingTurnout = Convert.ToDouble(m_dictVoting.Count) / Convert.ToDouble(m_iCurrPlayerCount);
                WritePluginConsole("^bVoting Ended. " + votingTurnout.ToString("##0%") + " of the players voted.", "Info", 2);
                logData["voting_turnout"] = votingTurnout;
            }
	    else
            {
                logData["voting_turnout"] = -1;
            }
 
            ArrayList options = new ArrayList();
            for (int i = 0; i < m_listMapOptions.Count; i++)
            {
                WritePluginConsole("^bVotes: ^6" + GetMapByFilename(m_listMapOptions[i]).PublicLevelName + " " + ConvertGamemodeToShorthand(GetMapByFilename(m_listMapOptions[i]).GameMode) + "^0: " + votes[i], "Info", 2);
                options.Add(
                    new Hashtable{
                        {"map_file_name", m_listMapOptions[i]},
                        {"map", GetMapByFilename(m_listMapOptions[i]).PublicLevelName},
                        {"mode", m_listGamemodeOptions[i]},
                        {"mode_shorthand", ConvertGamemodeToShorthand(GetMapByFilename(m_listMapOptions[i]).GameMode)},
                        {"votes", votes[i]}
                    }
                );
            }
            logData["options"] = options;

            this.ExecuteCommand("procon.protected.send", "admin.say", "VOTING ENDED!", "all");

            if ((m_dictVoting.Count + vipvotes) >= m_iVoteThres)
            {
                if (disableVoteResults == enumBoolYesNo.No)
                {
                    string mapandmode = GetMapByFilename(m_listMapOptions[winner]).PublicLevelName;
                    if (m_enumShowGamemode == enumBoolYesNo.Yes)
                    {
                        mapandmode += " " + ConvertGamemodeToShorthand(GetMapByFilename(m_listMapOptions[winner]).GameMode);
                    }

                    //if (votes[winner] <= m_dictVoting.Count)
                    //{

                    if (SayVoteResult == enumBoolYesNo.Yes)
                    {
                        this.ExecuteCommand("procon.protected.send", "admin.say", mapandmode + " Won with " + winningPercent.ToString("###%") + " of the votes (" + votes[winner] + "/" + (m_dictVoting.Count + vipvotes) + ")", "all");
                    }
                    if (YellVoteResult == enumBoolYesNo.Yes)
                    {
                        this.ExecuteCommand("procon.protected.send", "admin.yell", mapandmode + " Won with " + winningPercent.ToString("###%") + " of the votes (" + votes[winner] + "/" + (m_dictVoting.Count + vipvotes) + ")", m_iBannerYellDuration.ToString(), "all");
                    }

                    logData["event"] = "success";
                    logData["winner"] = new Hashtable{
                        {"map_file_name", m_listMapOptions[winner]},
                        {"map", GetMapByFilename(m_listMapOptions[winner]).PublicLevelName},
                        {"mode", m_listGamemodeOptions[winner]},
                        {"mode_shorthand", ConvertGamemodeToShorthand(GetMapByFilename(m_listMapOptions[winner]).GameMode)},
                        {"percent", winningPercent},
                        {"votes", votes[winner]}
                    };
                    
                    //}
                    //else
                    //{
                    //this.ExecuteCommand("procon.protected.send", "admin.say", mapandmode + " Won with " + winningPercent.ToString("###%") + " of the votes (" + votes[winner] + "/" + votes[winner] + ")", "all");
                    //}
                }
                WritePluginConsole("^b^6" + GetMapByFilename(m_listMapOptions[winner]).PublicLevelName + " " + ConvertGamemodeToShorthand(GetMapByFilename(m_listMapOptions[winner]).GameMode) + "^0 Won", "Info", 1);


                m_strNextMap = GetMapByFilename(m_listMapOptions[winner]).PublicLevelName;
                m_strNextMode = ConvertGamemodeToShorthand(GetMapByFilename(m_listMapOptions[winner]).GameMode);
                if (m_iNextMapDisplayInterval > 0)
                {
                    this.ExecuteCommand("procon.protected.tasks.add", "taskDisplayNextMap", "90", m_iNextMapDisplayInterval.ToString(), "-1", "procon.protected.plugins.call", "xVotemap", "DisplayNextMap", "all");
                }

                SetMap(m_listMapOptions[winner], m_listGamemodeOptions[winner]);

                // send infomation to UMM just in case maplist changes at EOR
                this.ExecuteCommand("procon.protected.plugins.call", "CUltimateMapManager", "VotedMapInfo", m_listMapOptions[winner], m_listGamemodeOptions[winner]);
            }
            else
            {
                this.ExecuteCommand("procon.protected.send", "admin.say", "Votemap failed. The total number votes (" + (m_dictVoting.Count + vipvotes) + ") did not exceed the threshold (" + m_iVoteThres + ")", "all");
                WritePluginConsole("^bVotemap failed^n. The total number votes (" + (m_dictVoting.Count + vipvotes) + ") did not exceed the threshold (" + m_iVoteThres + "), next map will not be changed.", "Info", 2);
		// vote failed = winner = next map in map list
                logData["event"] = "failed";
                logData["winner"] = new Hashtable{
                    {"map_file_name", m_listCurrMapList[this.m_iNextMapIndex].MapFileName},
                    {"map", GetMapByFilename(m_listCurrMapList[this.m_iNextMapIndex].MapFileName).PublicLevelName},
                    {"mode", m_listCurrMapList[this.m_iNextMapIndex].Gamemode},
                    {"mode_shorthand", ConvertGamemodeToShorthand(m_listCurrMapList[this.m_iNextMapIndex].Gamemode)},
                    {"percent", 0.0},
                    {"votes", 0}
                };

            }
            logData["total_votes"] = m_dictVoting.Count + vipvotes;

	    // Send data to  EventLogger if wanted
            if(this.logToEventLogger == enumBoolYesNo.Yes)
            {
                this.LogEvent("votemap_results", logData);
                
            }
        }

        public void DisplayNextMap(string target)
        {

            string mapandmode = m_strNextMap;
            if (m_enumShowGamemode == enumBoolYesNo.Yes)
            {
                mapandmode += " " + m_strNextMode;
            }

            if (SayNextMap == enumBoolYesNo.Yes)
            {
                if (target == "all")
                {
                    WritePluginConsole("Displayed next map", "Work", 3);
                    this.ExecuteCommand("procon.protected.send", "admin.say", "Next Map: " + mapandmode, "all");
                }
                else
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", "Next Map: " + mapandmode, "player", target);
                }
            }
            if (YellNextMap == enumBoolYesNo.Yes)
            {
                if (target == "all")
                {
                    WritePluginConsole("Displayed next map (yell)", "Work", 3);
                    this.ExecuteCommand("procon.protected.send", "admin.yell", "Next Map: " + mapandmode, m_iBannerYellDuration.ToString(), "all");
                }
                else
                {
                    this.ExecuteCommand("procon.protected.send", "admin.yell", "Next Map: " + mapandmode, m_iBannerYellDuration.ToString(), "player", target);
                }
            }
        }

        #endregion

        #region Tools

        private void SetMap(string mapName, string gamemode)
        {
            int index = 0;
            for (int i = 0; i < m_listCurrMapList.Count; i++)
            {
                if (m_listCurrMapList[i].MapFileName.CompareTo(mapName) == 0 && m_listCurrMapList[i].Gamemode.CompareTo(gamemode) == 0)
                {
                    index = i;
                    WritePluginConsole("Map found in current maplist: " + index, "Info", 5);
                    break;
                }
            }
            this.ExecuteCommand("procon.protected.send", "mapList.setNextMapIndex", index.ToString());
            this.ExecuteCommand("procon.protected.send", "mapList.getMapIndices");

            WritePluginConsole("Next map index set to: " + index, "Work", 5);
        }

        private string ConvertGamemodeToShorthand(string gamemode)
        {
            switch (gamemode)
            {
                case "Chainlink0":
                    //return "[CQ-L]";
                    return "[CL]";
                case "ConquestLarge0":
                case "CONQUEST":
                case "Levels/MP_009CQ":
                    //return "[CQ-L]";
                    return "[CQ]";
                case "ConquestSmall0":
                    //return "[CQ-S]";
                    return "[CQ]";
                case "ConquestAssaultSmall0":
                    //return "[CQA-S]";
                    return "[CQ]";
                case "ConquestAssaultSmall1":
                    //return "[CQA-S]";
                    return "[CQ]";
                case "ConquestAssaultLarge0":
                    //return "[CQA-L]";
                    return "[CQ]";
                case "RushLarge0":
                case "RUSH":
                    return "[R]";
                case "SquadRush0":
                case "SQRUSH":
                    return "[SQR]";
                case "SquadDeathMatch0":
                case "SQDM":
                    return "[SQDM]";
                case "TeamDeathMatch0":
                    return "[TDM]";
                case "Domination0":
                    //return "[CQ-D]";
                    return "[DOM]";
                case "TeamDeathMatchC0":
                    //return "[TDM-CQ]";
                    return "[TDM]";
                case "Elimination0":
                    return "[DF]";
                case "TankSuperiority0":
                    return "[TS]";
                case "Scavenger0":
                    return "[SC]";
                case "CaptureTheFlag0":
                    return "[CTF]";
                case "AirSuperiority0":
                    return "[AS]";
                case "Obliteration":
                    return "[OB]";
                case "GunMaster0":
                    return "[GM]";
                case "CarrierAssaultLarge0":
                    return "[CAL]";
                case "CarrierAssaultSmall0":
                    return "[CA]";

                default:
                    return "";
            }
        }

        private void WritePluginConsole(string message, string tag, int level)
        {
            if (tag.ToLower() == "error")
            {
                tag = "^8" + tag;
            }
            else if (tag.ToLower() == "work")
            {
                tag = "^4" + tag;
            }
            else
            {
                tag = "^5" + tag;
            }
            string line = "^b[" + this.GetPluginName() + " " + this.GetPluginVersion() + "] " + tag + ": ^0^n" + message;

            if (this.m_iDebugLevel >= level)
            {
                this.ExecuteCommand("procon.protected.pluginconsole.write", line);
            }

            //if (this.m_iDebugLevel >= level)
            //{
            //    this.ExecuteCommand("procon.protected.chat.write", line);
            //}

        }

        Random random = new Random(DateTime.Now.Millisecond + DateTime.Now.DayOfYear);
        private int RandomNumber(int min, int max)
        {
            return random.Next(min, max);
        }

        private List<int> PseudoRandom(int mid, int max, int randomness)
        {
            WritePluginConsole("PseudoRandom: Starting...", "Info", 4);
            List<int> selection = new List<int>();

            if (mid == max)
            {
                for (int i = 0; i < mid; i++)
                {
                    selection.Add(i);
                }
                WritePluginConsole("No flexiblity in displayed map options", "Info", 3);
            }
            else if (mid < max)
            {
                double[] rdmTable = RandomTable(mid, max, randomness);
                int rnd = 0;
                for (int i = 0; i < mid; i++)
                {
                    for (int m = 0; m < 100; m++) // attempt to find an unadded map
                    {
                        int testValue = -1;
                        rnd = RandomNumber(0, 1000);

                        testValue = FindInTable(rnd, rdmTable);
                        WritePluginConsole("Rnd: " + rnd + "result: " + testValue, "Info", 5);

                        if (testValue != -1 && !selection.Contains(testValue))
                        {
                            WritePluginConsole("Useable value found: " + testValue, "Info", 4);
                            selection.Add(testValue);
                            break;
                        }
                        else if (selection.Contains(testValue))
                        {
                            // WritePluginConsole("Duplicate found", "Info", 2);
                        }

                        // WritePluginConsole("Looping...", "Info", 2);
                    }
                }
            }

            WritePluginConsole("PseudoRandom: Done!", "Info", 4);

            return selection;
        }

        private int FindInTable(int value, double[] table)
        {
            int result = -1;
            for (int i = 0; i < table.Length; i++)
            {
                if (value < table[i])
                {
                    result = i;
                    break;
                }
            }
            return result;
        }

        private double[] RandomTable(int mid, int max, int randomness)
        {
            WritePluginConsole("RandomTable: Starting...", "Info", 4);
            double[] table = new double[max];

            double[] prob = new double[max];
            double probSum = 0;

            if (randomness == 0)
            {
                WritePluginConsole("RTable: If 1", "Info", 5);

                double step = 1000 / mid;
                for (int i = 0; i < max; i++)
                {
                    if (i < mid)
                    {
                        prob[i] = max;
                        probSum += prob[i];
                    }
                }
            }
            else if (randomness > 0 && randomness < 5)
            {
                WritePluginConsole("RTable: If 2", "Info", 5);

                double factor = (5 - (double)randomness) / 5;  // from 1.0 to 0.0

                double xStart = factor * mid;
                double xStop = mid + (1 - factor) * (max - mid);
                double diff = xStop - xStart;
                double grad = max / diff;

                double tri = diff * max * 0.5;

                for (int i = 0; i < max; i++)
                {
                    if (i + 1 < xStart)
                    {
                        prob[i] = max;

                        WritePluginConsole("RTable: Prenorm Block:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                    }
                    else if (i > xStop)
                    {
                        break;  // do nothing
                    }
                    else
                    {
                        if (i < xStart)
                        {
                            prob[i] = (xStart - i) * max;
                            if (i + 1 > xStop)
                            {
                                prob[i] += tri;

                                WritePluginConsole("RTable: Prenorm End:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                            }
                            else
                            {
                                prob[i] += tri - ((xStop - (i + 1)) * (xStop - (i + 1)) * grad * 0.5);

                                WritePluginConsole("RTable: Prenorm Stub:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                            }
                        }
                        else if (xStart < i && xStop < i + 1)
                        {
                            prob[i] += (xStop - i) * (xStop - i) * grad * 0.5;


                            WritePluginConsole("RTable: Prenorm Tip:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                        }
                        else
                        {
                            prob[i] += (xStop - i) * (xStop - i) * grad * 0.5 - (xStop - (i + 1)) * (xStop - (i + 1)) * grad * 0.5;

                            WritePluginConsole("RTable: Prenorm Chunk:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                        }
                    }
                    probSum += prob[i];
                }
            }
            else if (randomness >= 5 && randomness <= 10)
            {
                WritePluginConsole("RTable: If 3", "Info", 5);

                double factor = (10 - (double)randomness) / 5;  // from 1.0 to 0.0
                double smlTri = 0.5 * factor;
                double baseSqr = max * (1 - factor);

                WritePluginConsole("RTable: Tri:" + string.Format("{0:0.00}", smlTri) + "base:" + string.Format("{0:0.0}", baseSqr) + "factor:" + string.Format("{0:0.0}", factor), "Info", 5);

                for (int i = 0; i < max; i++)
                {
                    prob[i] = (max * 2 - i * 2 - 1) * smlTri + baseSqr;
                    probSum += prob[i];
                    WritePluginConsole("RTable: Prenorm:" + i.ToString() + ": " + string.Format("{0:0.0}", prob[i]), "Info", 5);
                }
            }
            // Normalise
            for (int i = 0; i < max; i++)
            {
                prob[i] = prob[i] * 1000 / probSum;
                WritePluginConsole("Map " + i.ToString() + " Probability: " + string.Format("{0:0.0}%", prob[i] / 10), "Info", 4);
            }

            table[0] = prob[0];
            WritePluginConsole("RTable Sum: " + 0.ToString() + ": " + string.Format("{0:0.0}", table[0]), "Info", 5);
            for (int i = 1; i < max; i++)
            {
                table[i] = table[i - 1] + prob[i];
                WritePluginConsole("RTable Sum: " + i.ToString() + ": " + string.Format("{0:0.0}", table[i]), "Info", 5);
            }

            WritePluginConsole("RandomTable: Done!", "Info", 4);
            return table;
        }

        public int MaxIndex(int[] array)
        {
            int maxIndex = -1;
            int maxValue = -1;
            int index = 0;
            List<int> draws = new List<int>();

            foreach (int value in array)
            {
                if (value.CompareTo(maxValue) > 0)
                {
                    maxIndex = index;
                    maxValue = value;

                    draws = new List<int>();
                    draws.Add(index);
                }
                else if (value.CompareTo(maxValue) == 0)
                {
                    draws.Add(index);
                }
                index++;
            }

            if (draws.Count > 1)
            {
                maxIndex = draws[RandomNumber(0, draws.Count)];
                WritePluginConsole("Winning vote tie, ^6" + GetMapByFilename(m_listMapOptions[maxIndex]).PublicLevelName + " " + ConvertGamemodeToShorthand(GetMapByFilename(m_listMapOptions[maxIndex]).GameMode) + "^0 randomly selected as winner.", "Info", 3);
            }

            return maxIndex;
        }

        public double GetMin(double[] DoubleCollection)
        {
            double min = double.MaxValue;
            foreach (double i in DoubleCollection)
            {
                if (i < min)
                {
                    min = i;
                }
            }
            return min;
        }

        public bool ListsEqual(List<MaplistEntry> a, List<MaplistEntry> b)
        {
            if (a.Count != b.Count)
            {
                WritePluginConsole("Maplist counts not equal: " + a.Count + " != " + b.Count, "Info", 5);
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].MapFileName != b[i].MapFileName || a[i].Gamemode != b[i].Gamemode || a[i].Index != b[i].Index || a[i].Rounds != b[i].Rounds)
                {
                    WritePluginConsole("Maps not equal @ " + i + " A " + a[i].MapFileName + " " + a[i].Gamemode + " " + a[i].Rounds, "Info", 5);
                    WritePluginConsole("Maps not equal @ " + i + " B " + b[i].MapFileName + " " + b[i].Gamemode + " " + b[i].Rounds, "Info", 5);
                    return false;
                }
            }

            return true;
        }

        public string ToReadableString(TimeSpan span)
        {
            string formatted = string.Format("{0}{1}{2}{3}",
                span.Days > 0 ? string.Format("{0:0} days, ", span.Days) : string.Empty,
                span.Hours > 0 ? string.Format("{0:0} hours, ", span.Hours) : string.Empty,
                span.Minutes > 0 ? string.Format("{0:0} minutes, ", span.Minutes) : string.Empty,
                span.Seconds > 0 ? string.Format("{0:0} seconds", span.Seconds) : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

            return formatted;
        }

        //private int MeasureText(string text)
        //{
        //    System.Drawing.Font arialBold = new System.Drawing.Font("Tahoma", 12.0F);
        //    System.Drawing.Size textSize = TextRenderer.MeasureText(text, arialBold);
        //    return textSize.Width;
        //}

        private string GeneratePadding(string text, double length)
        {
            string padding = "  ";
            double txtLength = 0.0;

            char[] chars = text.ToCharArray();

            foreach (char c in chars)
            {
                double value = 1.0;
                if (m_dictLetterSizes.TryGetValue(c, out value))
                {
                    txtLength += value;
                }
                else
                {
                    txtLength += 1.0;
                }
            }

            double whitespaceLength = length - txtLength;
            double widthofspace = 0;
            m_dictLetterSizes.TryGetValue(' ', out widthofspace);
            double numOfSpaces = whitespaceLength / widthofspace;
            int iSpaces = (int)Math.Round(numOfSpaces);
            if (iSpaces > 0)
            {
                padding = new String(' ', iSpaces);
            }

            return padding;
        }

        #endregion

        public override void OnReservedSlotsList(List<string> soldierNames)
        {
            if (this.syncreservedslots == enumBoolYesNo.Yes)
            {
                foreach (string vipplayer in soldierNames)
                {
                    if (!this.vips.Contains(vipplayer))
                    {
                        this.vips.Add(vipplayer);
                    }
                }
                //}
                List<string> playerstoremove = new List<string>();
                foreach (string vipplayer in vips)
                {
                    if (!soldierNames.Contains(vipplayer))
                    {
                        playerstoremove.Add(vipplayer);
                    }
                }
                foreach (string player in playerstoremove)
                {
                    vips.Remove(player);
                }
            }
        }

        private void LogEvent(String type, Hashtable logData)
        {
            String payload = JSON.JsonEncode(logData);
            WritePluginConsole("Sent to EventLogger: " + payload, "Info", 3);
            this.ExecuteCommand("procon.protected.plugins.call", "EventLogger", "SqlLog", type, "xVotemap", payload);
        }
    }
}
