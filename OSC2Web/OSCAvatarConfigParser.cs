using System.Text.RegularExpressions;
using Newtonsoft.Json;
using OSC2Web.Sterlizable;

namespace OSC2Web
{
    // You may be asking why does this exist its because vrchat is stupid and default values can be null sometimes for whatever reason.
    internal static class OSCAvatarConfigParser
    {
        internal static AvatarConfig GetAvatarConfig(string Id)
        {
            var Config = new AvatarConfig();
            GetCurrentUserId();
            string VRCavatrConfig = GetAvatarFile(Id);
            if (!string.IsNullOrEmpty(VRCavatrConfig))
            {
                Config = JsonConvert.DeserializeObject<AvatarConfig>(VRCavatrConfig);
            }
            return Config;
        }
        private static string GetAvatarFile(string avatarId)
        {
            string oscFolder = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          @"..\LocalLow\VRChat\VRChat\OSC"
      );

            string fullPath = null;
            string masterJson = Path.Combine(oscFolder, Config.UserId, "Avatars", avatarId + ".json");
            if (File.Exists(masterJson))
                return File.ReadAllText(masterJson);

            foreach (var userDir in Directory.GetDirectories(Path.GetFullPath(oscFolder)))
            {

                string avatarPath = Path.Combine(userDir, "Avatars", avatarId + ".json");
                if (File.Exists(avatarPath))
                {
                    fullPath = avatarPath;
                    break;
                }
            }

            if (fullPath != null)
            {
                string jsonContent = File.ReadAllText(fullPath);
                return jsonContent;
            }
            else
            {
                Console.WriteLine("Avatar JSON file not found.");
                return null;
            }
        }
        internal static AvatarConfig SetDefaultStatesFromConfig(this AvatarConfig avatarConfig)
        {
            string oscFolder = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"..\LocalLow\VRChat\VRChat\LocalAvatarData\", Config.UserId, avatarConfig.Id
            ));

            if (!File.Exists(oscFolder))
                return avatarConfig;

            var avatarData = JsonConvert.DeserializeObject<VRCAvatarConfig>(File.ReadAllText(oscFolder));
            if (avatarData?.animationParameters == null)
                return avatarConfig;

            foreach (var parameter in avatarConfig.parameters)
            {
                var sourceParam = avatarData.animationParameters
                    .FirstOrDefault(x => x.name == parameter.Name); // assuming case-sensitive match

                if (sourceParam?.value == null)
                    continue;

                string type = parameter.Input.Type?.ToLower();
                var rawValue = sourceParam.value;

                switch (type)
                {
                    case "bool":
                        if (rawValue is bool b)
                            parameter.Value = b;
                        else if (rawValue is int i)
                            parameter.Value = i != 0;
                        else if (rawValue is long l)
                            parameter.Value = l != 0;
                        else if (bool.TryParse(rawValue.ToString(), out bool boolVal))
                            parameter.Value = boolVal;
                        break;

                    case "int":
                        if (rawValue is int intVal)
                            parameter.Value = intVal;
                        else if (int.TryParse(rawValue.ToString(), out int parsedInt))
                            parameter.Value = parsedInt;
                        break;

                    case "float":
                        if (rawValue is float floatVal)
                            parameter.Value = floatVal;
                        else if (float.TryParse(rawValue.ToString(), out float parsedFloat))
                            parameter.Value = parsedFloat;
                        break;

                    default:
                        continue;
                }
            }

            return avatarConfig;
        }

        internal static AvatarConfig StripInvalidParams(this AvatarConfig avatarConfig)
        {

            avatarConfig.parameters.RemoveAll(param =>
            {
                bool shouldRemove = param == null || string.IsNullOrEmpty(param.Name) || param.Input == null || param.Output == null;
                if (shouldRemove)
                {
                    if (Config.Instance.debug)
                   Console.WriteLine($"Removing invalid parameter: {param?.Name}");
                }
                return shouldRemove;
            });

            // Set default values
            foreach (var param in avatarConfig.parameters)
            {
                if (param.Input.Type.Equals("bool", StringComparison.OrdinalIgnoreCase))
                {
                    if (param.Value == null)
                        param.Value = false;
                }
                else if (param.Input.Type.Equals("float", StringComparison.OrdinalIgnoreCase))
                {
                    if (param.Value == null)
                        param.Value = 0f;
                }
                else if (param.Input.Type.Equals("int", StringComparison.OrdinalIgnoreCase))
                {
                    if (param.Value == null)
                        param.Value = 0;
                }
            }
            return avatarConfig;
        }

        static string[] blacklistedItems = new[] { "FT/v2", "VFH/Version/", "VF ", "IsGrabbed", "IsPosed", };
        static string[] nsfwItems = new[] { "WH Lollipop", "OGB", "Assjob", "Pussy", "Anal", "NSFW", "SFX_On", "job", "autoMode", "stealth", "autoMode", "multi", "Feet/Steppies", "SPS", "Frotting", "Wsome Lollipop", "PCS" };

        internal static AvatarConfig StripParams(this AvatarConfig avatarConfig)
        {
            // strip all non bools for now.
            /* avatarConfig.parameters.RemoveAll(param =>
             {
                 bool shouldRemove = param.Input.Type.ToLower() != "bool";
                 return shouldRemove;
             });
            */
            avatarConfig.parameters.RemoveAll(param =>
            {
                bool shouldRemove = blacklistedItems.Any(item => param.Name.Contains(item, StringComparison.OrdinalIgnoreCase));
                return shouldRemove;
            });

            if (Config.Instance.removeBlacklistedItems)
                avatarConfig.parameters.RemoveAll(param =>
                {
                    bool shouldRemove = nsfwItems.Any(item => param.Name.Contains(item, StringComparison.OrdinalIgnoreCase));
                    return shouldRemove;
                });

            avatarConfig.parameters.RemoveAll(param =>
            {
                bool shouldRemove = Config.Instance.blacklistedItems.Any(item => param.Name.Contains(item, StringComparison.OrdinalIgnoreCase));
                return shouldRemove;
            });
            return avatarConfig;
        }


        internal static void GetCurrentUserId()
        {
            string oscFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"..\LocalLow\VRChat\VRChat\"
            );

            if (!Directory.Exists(oscFolder))
            {
                Console.WriteLine("OSC folder does not exist.");
                return;
            }

            var txtFiles = Directory.GetFiles(oscFolder, "*.txt");

            if (txtFiles.Length == 0)
            {
                Console.WriteLine("No .txt files found in the OSC folder.");
                return;
            }

            string lastCreatedFile = txtFiles
                .OrderBy(f => File.GetCreationTime(f))
                .Last();

            try
            {
                using FileStream stream = new FileStream(
                    lastCreatedFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite
                );
                using StreamReader reader = new StreamReader(stream);
                string fileContent = reader.ReadToEnd();

                // Match the line with "User Authenticated: ..." and extract usr_GUID from within parentheses
                var match = Regex.Match(
                    fileContent,
                    @"User Authenticated:.*?\((usr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)"
                );

                if (match.Success)
                {
                    Config.UserId = match.Groups[1].Value;
                    Console.WriteLine($"Current User ID: {Config.UserId}");
                }
                else
                {
                    Console.WriteLine("No authenticated user ID found in the file.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file: {ex.Message}");
            }
        }

    }

}