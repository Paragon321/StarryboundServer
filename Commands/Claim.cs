using com.avilance.Starrybound.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using com.avilance.Starrybound.Extensions;

namespace com.avilance.Starrybound.Commands
{
    class Claim:CommandBase
    {
        public Claim(Client client)
        {
            this.name = "claim";
            this.HelpText = ": Allows you to claim a planet. <release> <num> releases a claim. <warp> <num> warp to claimed planet.";
            this.Permission = new List<String>();
            this.client = client;
            this.player = client.playerData;
        }

        public override bool doProcess(string[] args)
        {
            if (args.Length==0)
                return doStakeClaimProcess();

            //A players first claim is claim number 1
            int claimNum=1;
            if (args.Length>1)
            {
                if (!int.TryParse(args[1], out claimNum))
                {
                    client.sendChatMessage("Invalid Claim Number.");
                    return false;
                }
            }
            switch(args[0].ToLower().Trim())
            {
                case "release":
                    return doReleaseClaimProcess(claimNum);
                case "warp":
                    return doWarpClaimProcess(claimNum);
                case "list":
                    return doListClaimProcess();
            }
            client.sendChatMessage("Unknown Claim command.");
            return false;
        }
        public bool doStakeClaimProcess()
        {
            Claims.ClaimResult ret = Claims.ClaimResult.OK;
            if (!hasPermission()) { permissionError(); return false; }
            var currLoc = this.player.loc;
            if (player.inPlayerShip)
            {
                client.sendChatMessage("You can not claim your ship.");
                return false;
            }
            if(StarryboundServer.spawnPlanet.Equals(player.loc))
            {
                client.sendChatMessage("You can not claim the spawn planet.");
                return false;
            }
            if (!player.hasPermission("world.build"))
            {
                client.sendChatMessage("You can not claim if you can not build.");
                return false;
            }
            ret = Claims.StakeClaim(player, currLoc);
            if (ret == Claims.ClaimResult.OK)
            {
                client.sendChatMessage("You have sucessfully staked your claim on this planet.");
                return true;
            }

            switch(ret)
            {
                case Claims.ClaimResult.LocationAlreadyClaimedByOther:
                    client.sendChatMessage("This planet has already been claimed by another player.");
                    break;
                case Claims.ClaimResult.LocationAlreadyClaimedByPlayer:
                    client.sendChatMessage("You have already claimed this planet");
                    break;
                case Claims.ClaimResult.MaxClaimsExceeded:
                    client.sendChatMessage("You have already staked your allowed number of claims.");
                    break;
            }
            return false;
        }

        public bool doReleaseClaimProcess(int claimNum)
        {
            var claims = Claims.GetClaims(player);
            if (claims.Count<claimNum) 
            {
                client.sendChatMessage("You do not have that claim to release.");
                return false;
            }
            if(Claims.ReleaseClaim(player, claims[claimNum-1]))
            {
                client.sendChatMessage("You have sucesfully released a claim.");
                return true;
            }
            client.sendChatMessage("Something?");
            return false;
        }

        public bool doWarpClaimProcess(int claimNum)
        {
            if (!this.player.inPlayerShip)
            {
                client.sendChatMessage("You must be on your ship to warp");
                return false;
            }

            var claims = Claims.GetClaims(this.player);
            if (claims.Count<claimNum)
            {
                client.sendChatMessage("You do not have that claim to warp to.");
                return false;
            }
            var warpCoord = claims[claimNum - 1];
            MemoryStream packetWarp = new MemoryStream();
            BinaryWriter packetWrite = new BinaryWriter(packetWarp);
            uint warp = (uint)WarpType.MoveShip;
            packetWrite.WriteBE(warp);
            packetWrite.Write(warpCoord);
            packetWrite.WriteStarString("");
            client.sendServerPacket(Packet.WarpCommand, packetWarp.ToArray());
            return true;
        }

        public bool doListClaimProcess()
        {
            var v = Claims.GetClaims(this.player);
            int i = 0;
            StringBuilder sb=new StringBuilder();
            foreach (var loc in v)
            {
                sb.AppendFormat("{0}: {1}\n", ++i, loc);
            }
            if (i==0)
            {
                sb.AppendLine("You have staked no claims.");
            }
            client.sendChatMessage(sb.ToString());
            return true;
        }
    }

}
