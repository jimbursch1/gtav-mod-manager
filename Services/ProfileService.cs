using System;
using System.Collections.Generic;
using System.Linq;
using GtavModManager.Core;
using GtavModManager.Data;

namespace GtavModManager.Services
{
    public class ProfileService
    {
        private readonly ProfileRepository _repo;
        private readonly QuarantineService _quarantine;
        private List<Profile> _profiles = new List<Profile>();

        public ProfileService(ProfileRepository repo, QuarantineService quarantine)
        {
            _repo = repo;
            _quarantine = quarantine;
        }

        public void Load()
        {
            _profiles = _repo.Load();
        }

        public void Save()
        {
            _repo.Save(_profiles);
        }

        public IReadOnlyList<Profile> GetAllProfiles() => _profiles.AsReadOnly();

        public Profile GetActiveProfile(string activeProfileId) =>
            _profiles.FirstOrDefault(p => p.Id == activeProfileId);

        public Profile CreateProfile(string name, IReadOnlyList<Mod> currentEnabledMods)
        {
            var profile = new Profile
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                EnabledModIds = currentEnabledMods.Select(m => m.Id).ToList(),
                LoadOrder = currentEnabledMods
                    .Where(m => m.LoadOrder > 0)
                    .ToDictionary(m => m.Id, m => m.LoadOrder)
            };
            _profiles.Add(profile);
            return profile;
        }

        public OperationResult SwitchProfile(string profileId, IReadOnlyList<Mod> allMods)
        {
            var target = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (target == null)
                return OperationResult.Fail($"Profile '{profileId}' not found.");

            var targetEnabledIds = new HashSet<string>(target.EnabledModIds);
            var currentEnabled = allMods.Where(m => m.Status == ModStatus.Enabled).ToList();
            var currentEnabledIds = new HashSet<string>(currentEnabled.Select(m => m.Id));

            var toDisable = currentEnabled.Where(m => !targetEnabledIds.Contains(m.Id)).ToList();
            var toEnable = allMods.Where(m => targetEnabledIds.Contains(m.Id) && m.Status == ModStatus.Disabled).ToList();

            // Disable first
            var disabledSoFar = new List<Mod>();
            foreach (var mod in toDisable)
            {
                var result = _quarantine.DisableMod(mod);
                if (!result.Success)
                {
                    // Rollback: re-enable what we disabled
                    foreach (var re in disabledSoFar)
                        _quarantine.EnableMod(re);
                    return OperationResult.Fail($"Failed to disable '{mod.Name}': {result.ErrorMessage}");
                }
                disabledSoFar.Add(mod);
            }

            // Enable
            var enabledSoFar = new List<Mod>();
            foreach (var mod in toEnable)
            {
                var result = _quarantine.EnableMod(mod);
                if (!result.Success)
                {
                    // Rollback: disable what we enabled, re-disable what we undisabled
                    foreach (var re in enabledSoFar)
                        _quarantine.DisableMod(re);
                    foreach (var re in disabledSoFar)
                        _quarantine.EnableMod(re);
                    return OperationResult.Fail($"Failed to enable '{mod.Name}': {result.ErrorMessage}");
                }
                enabledSoFar.Add(mod);
            }

            target.LastActivated = DateTime.UtcNow;
            return OperationResult.Ok();
        }

        public void DeleteProfile(string profileId)
        {
            _profiles.RemoveAll(p => p.Id == profileId);
        }
    }
}
