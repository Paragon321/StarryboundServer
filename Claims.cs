using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace com.avilance.Starrybound
{
    public static class Claims
    {
        static Dictionary<String, List<Extensions.WorldCoordinate>> _playerToSystem=new Dictionary<string,List<Extensions.WorldCoordinate>>();
        static Dictionary<Extensions.WorldCoordinate, String> _SystemToPlayer = new Dictionary<Extensions.WorldCoordinate, string>(new Extensions.WorldCoordEqualityComparer());
        static Mutex stakeClaimMutex=new Mutex(false);
        public enum ClaimResult
        {
            OK=0,
            MaxClaimsExceeded=1,
            LocationAlreadyClaimedByPlayer=2,
            LocationAlreadyClaimedByOther =4
        }

        public static String GetStakeOwner(Extensions.WorldCoordinate loc)
        {
            if (_SystemToPlayer.ContainsKey(loc))
                return _SystemToPlayer[loc];
            return null;
        }

        public static List<Extensions.WorldCoordinate> GetClaims(PlayerData plyr)
        {
            if (_playerToSystem.ContainsKey(plyr.name))
                return _playerToSystem[plyr.name];
            return null;
        }

        public static ClaimResult StakeClaim(PlayerData player, Extensions.WorldCoordinate loc)
        {
            stakeClaimMutex.WaitOne();
            if (!_playerToSystem.ContainsKey(player.name))
                _playerToSystem.Add(player.name, new List<Extensions.WorldCoordinate>());
            var currPlyClaims = _playerToSystem[player.name];

            if (_SystemToPlayer.ContainsKey(loc))
            {
                stakeClaimMutex.ReleaseMutex();
                if (_SystemToPlayer[loc] == player.name)
                    return ClaimResult.LocationAlreadyClaimedByPlayer;
                return ClaimResult.LocationAlreadyClaimedByOther;
            }

            if (currPlyClaims.Count >= player.MaxClaims)
            {
                stakeClaimMutex.ReleaseMutex();
                return ClaimResult.MaxClaimsExceeded;
            }
            _playerToSystem[player.name].Add(loc);
            _SystemToPlayer.Add(loc, player.name);
            SaveClaims();
            stakeClaimMutex.ReleaseMutex();
            return ClaimResult.OK;
        }

        public static bool ReleaseClaim(PlayerData player, Extensions.WorldCoordinate loc)
        {
            try
            {
                stakeClaimMutex.WaitOne();
                if (_SystemToPlayer.ContainsKey(loc))
                {
                    if (_SystemToPlayer[loc] == player.name)
                    {
                        _SystemToPlayer.Remove(loc);
                        _playerToSystem[player.name].Remove(loc);
                        SaveClaims();
                        stakeClaimMutex.ReleaseMutex();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                StarryboundServer.logException("Could not release claim to location {0} for player {1}", loc, player.uuid);
                stakeClaimMutex.ReleaseMutex();
                return false;
            }
            return false;
        }
        
        public static void LoadClaims()
        {
            try
            {
                String claimPath = Path.Combine(StarryboundServer.SavePath, "claims.json");
                StarryboundServer.logInfo("Loading players claims from file " + claimPath);
                if (File.Exists(claimPath))
                {
                    String serializedData = String.Empty;
                    using (StreamReader rdr = new StreamReader(claimPath))
                    {
                        serializedData = rdr.ReadLine();
                    }
                    if (!String.IsNullOrEmpty(serializedData))
                        _playerToSystem = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<String, List<Extensions.WorldCoordinate>>>(serializedData);

                    foreach (String plyUUID in _playerToSystem.Keys)
                    {
                        foreach (Extensions.WorldCoordinate coord in _playerToSystem[plyUUID])
                        {
                            _SystemToPlayer.Add(coord, plyUUID);
                        }
                    }
                }
            } catch(Exception e)
            {
                StarryboundServer.logException("Unable to load claims from claims.json: {0}", e.Message);
            }
        }
        public static void SaveClaims()
        {
            try
            {
                using (StreamWriter wtr=new StreamWriter(Path.Combine(StarryboundServer.SavePath,"claims.json")))
                    wtr.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(_playerToSystem));
            }catch (Exception e)
            {
                StarryboundServer.logException("Unable to write claims to claims.json: {0}",e.Message);
            }

            return;
        }
    }
}
