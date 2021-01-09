using InfinityScript;
using System;
using System.Collections.Generic;
using System.Text;
using static InfinityScript.GSCFunctions;

namespace IW4MAdmin
{
    public class Main : BaseScript
    {
        private readonly Dictionary<string, ClientMeta> _meta;
        private const int SvMaxStoredFrames = 4;
        private const int SvFrameWaitTime = 50;
        public Main()
        {
            Utilities.ExecuteCommand("set sv_customcallbacks 1");
            _meta = new Dictionary<string, ClientMeta>();
            PlayerConnected += OnPlayerConnect;
            InfinityScript.Log.Write(LogLevel.Info, "AntiCheat by RaidMax, ported to IS 1.5.0 by Diavolo#6969");
        }


        public void OnPlayerConnect(Entity player)
        {
            var meta = new ClientMeta();
            var frames = new Vector3[SvMaxStoredFrames];
            meta.Set("anglePositions", frames);
            meta.Set("currentAnglePosition", 0);
            _meta.Add(player.HWID, meta);
            OnInterval(SvFrameWaitTime, () =>
            {
                if (!_meta.ContainsKey(player.HWID))                    
                    return false;
                UpdateViewAngleCache(player);
                return true;
            });
            
        }

        public override void OnPlayerDisconnect(Entity player)
        {
            _meta.Remove(player.HWID);
        }

        public override void OnPlayerKilled(Entity self, Entity inflictor, Entity attacker, int damage, string mod, string weapon, Vector3 dir, string hitLoc)
        {
            Process_Hit("Kill", self, attacker, inflictor, hitLoc, mod, damage, weapon);
        }

        public override void OnPlayerDamage(Entity self, Entity inflictor, Entity attacker, int damage, int dFlags, string mod, string weapon, Vector3 point, Vector3 dir, string hitLoc)
        {
            // todo: this accounts for team damage.. will need to figure out how to prevent
            //if (level.teamBased && isDefined(attacker) && (self != attacker) && isDefined(attacker.team) && (self.pers["team"] == attacker.team))
            //{
            //    return;
            //}

            if (self.Health - damage > 0)
            {
                Process_Hit("Damage", self, attacker, inflictor, hitLoc, mod, damage, weapon);
            }
        }

        private void WaitForAdditionalAngles(Entity self, string logString, int beforeFrameCount, int afterFrameCount)
        {
            int currentIndex = _meta[self.HWID].Get<int>("currentAnglePosition");

            GameLogger.Write("Going to wait for additional angles at -> {0}", GetTime());

            AfterDelay(SvFrameWaitTime * afterFrameCount, () =>
            {
                // _gameLog.Write("Finished waiting for angles at -> {0}", Call<int>("getTime"));
                var clientMeta = _meta[self.HWID];
                var anglePositions = clientMeta.Get<Vector3[]>("anglePositions");
                var angleSnapshot = new Vector3[anglePositions.Length];

                for (int j = 0; j < anglePositions.Length; j++)
                {
                    angleSnapshot[j] = anglePositions[j];
                }

                StringBuilder anglesStr = new StringBuilder();
                int collectedFrames = 0;
                int i = currentIndex - beforeFrameCount;

                while (collectedFrames < beforeFrameCount)
                {
                    int fixedIndex = i;

                    if (i < 0)
                    {
                        fixedIndex = angleSnapshot.Length - Math.Abs(i);
                    }

                    anglesStr.Append(angleSnapshot[fixedIndex] + ":");
                    collectedFrames++;
                    i++;
                }

                if (i == currentIndex)
                {
                    anglesStr.Append(angleSnapshot[i] + ":");
                    i++;
                }

                collectedFrames = 0;

                while (collectedFrames < afterFrameCount)
                {
                    int fixedIndex = i;
                    if (i > angleSnapshot.Length - 1)
                    {
                        fixedIndex = i % angleSnapshot.Length;
                    }
                    anglesStr.Append(angleSnapshot[fixedIndex] + ":");
                    collectedFrames++;
                    i++;
                }

                GameLogger.Write($"{logString};{anglesStr.ToString()};0;0");
            });
        }

        private bool UpdateViewAngleCache(Entity client)
        {
            var clientMeta = _meta[client.HWID];
            var anglePositions = clientMeta.Get<Vector3[]>("anglePositions");
            int currentAnglePosition = clientMeta.Get<int>("currentAnglePosition");
            anglePositions[currentAnglePosition] = client.GetPlayerAngles();
            currentAnglePosition = (currentAnglePosition + 1) % SvMaxStoredFrames;
            clientMeta.Set("currentAnglePosition", currentAnglePosition);
            return true;
        }

        private bool IsRealPlayer(Entity ent) => ent != null && IsPlayer(ent);

        private string WeaponInventoryTypeString(string weaponName) => WeaponInventoryType(weaponName);

        private bool IsAirdropMarker(string weaponName)
        {
            switch (weaponName)
            {
                case "airdrop_marker_mp":
                case "airdrop_mega_marker_mp":
                case "airdrop_sentry_marker_mp":
                case "airdrop_juggernaut_mp":
                case "airdrop_juggernaut_def_mp":
                    return true;
                default:
                    return false;
            }
        }

        private bool IsKillstreakWeapon(string weapon)
        {
            if (string.IsNullOrEmpty(weapon))
            {
                return false;
            }

            if (weapon == "none")
                return false;

            var tokens = weapon.Split('_');
            bool foundSuffix = false;

            //this is necessary because of weapons potentially named "_mp(somthign)" like the mp5
            if (weapon != "destructible_car" && weapon != "barrel_mp")
            {
                foreach (string token in tokens)
                {
                    if (token == "mp")
                    {
                        foundSuffix = true;
                        break;
                    }
                }

                if (!foundSuffix)
                {
                    weapon += "_mp";
                }
            }

            if (weapon.Contains("destructible"))
                return false;

            if (weapon.Contains("killstreak"))
                return true;

            if (IsAirdropMarker(weapon))
                return true;

            /* TODO: we can't check on level fields from here
            if (isDefined(level.killstreakWeildWeapons[weapon]))
                return true;*/

            if (!string.IsNullOrEmpty(WeaponInventoryTypeString(weapon)) && WeaponInventoryTypeString(weapon) == "exclusive" && (weapon != "destructible_car" && weapon != "barrel_mp"))
                return true;

            return false;
        }

        private void Process_Hit(string type, Entity self, Entity attacker, Entity inflictor, string sHitLoc, string sMeansOfDeath, int iDamage, string sWeapon)
        {

            if (sMeansOfDeath == "MOD_FALLING" || (!IsRealPlayer(attacker) && !IsRealPlayer(inflictor)))
            {
                return;
            }

            var victim = self;
            var _attacker = attacker;

            if (!IsRealPlayer(attacker) && IsRealPlayer(inflictor))
            {
                _attacker = inflictor;
            }

            else if (!IsRealPlayer(attacker) && sMeansOfDeath == "MOD_FALLING")
            {
                _attacker = victim;
            }

            var location = victim.GetTagOrigin(HitLocationToBone(sHitLoc));
            bool isKillstreakKill = !IsRealPlayer(attacker) || IsKillstreakWeapon(sWeapon);

            if (isKillstreakKill)
            {
                GameLogger.Write("{0} appears to be a killstreak weapon", sWeapon);
            }

            string attackerGuid = attacker.HWID;
            string victimGuid = victim.HWID;
            var attackerPos = _attacker.GetTagOrigin("tag_eye");
            var attackerViewPos = _attacker.GetPlayerAngles();
            int time = GetTime();
            string logLine = $"Script{type};{attackerGuid};{victimGuid};{attackerPos};{location};{iDamage};{sWeapon};{sHitLoc};{sMeansOfDeath};{attackerViewPos};{time};{(isKillstreakKill ? 1 : 0)};0;0;0";
            WaitForAdditionalAngles(self, logLine, 2, 2);
        }

        private string HitLocationToBone(string hitloc)
        {
            switch (hitloc)
            {
                case "helmet":
                    return "j_helmet";
                case "head":
                    return "j_head";
                case "neck":
                    return "j_neck";
                case "torso_upper":
                    return "j_spineupper";
                case "torso_lower":
                    return "j_spinelower";
                case "right_arm_upper":
                    return "j_shoulder_ri";
                case "left_arm_upper":
                    return "j_shoulder_le";
                case "right_arm_lower":
                    return "j_elbow_ri";
                case "left_arm_lower":
                    return "j_elbow_le";
                case "right_hand":
                    return "j_wrist_ri";
                case "left_hand":
                    return "j_wrist_le";
                case "right_leg_upper":
                    return "j_hip_ri";
                case "left_leg_upper":
                    return "j_hip_le";
                case "right_leg_lower":
                    return "j_knee_ri";
                case "left_leg_lower":
                    return "j_knee_le";
                case "right_foot":
                    return "j_ankle_ri";
                case "left_foot":
                    return "j_ankle_le";
                default:
                    return "tag_origin";
            }
        }
    }
}