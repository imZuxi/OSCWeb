using BuildSoft.VRChat.Osc;
using BuildSoft.VRChat.Osc.Avatar;
using Newtonsoft.Json;
using OSC2Web.Sterlizable;
namespace OSC2Web
{
    internal class Program
    {
        static AvatarConfig avatarConfig;
        static async Task Main(string[] args)
        {
            Console.Clear();
            Console.CancelKeyPress += new ConsoleCancelEventHandler((e, t) => { OnExit(); });
            OscConnectionSettings.SendPort = 9000; // Set the port for sending OSC messages
            OscConnectionSettings.ReceivePort = 9001; // Set the port for receiving OSC messages
            OscAvatarUtility.Initialize();
            WebSocketManager.CreateSocket();
            
            



            /*   var config = OscAvatarConfig.CreateAtCurrent();

               if (config == null)
               {
                   Console.WriteLine("Failed to get the current avatar, Do \"Reset Avatar\" or start VRChat.");
               }
            */
            OscAvatarUtility.AvatarChanged += OnAvatarChanged;
            Console.WriteLine("waiting for vrchat to say something... try changing avatars");
            // Wait until you can get an avatar config.
            do { } while (true);
        }


        private static void OnAvatarChanged(OscAvatar sender, ValueChangedEventArgs<OscAvatar> e)
        {
            new AvatarConfig().UpdateParams();
            OscAvatarConfig config = e.NewValue.ToConfig();
            avatarConfig = OSCAvatarConfigParser.GetAvatarConfig(config.Id).StripInvalidParams().SetDefaultStatesFromConfig();

            foreach (var item in config.Parameters)
            {
                if (item.Value is bool)
                {
                    try
                    {
                        avatarConfig.parameters.Where(x => x.Name == item.Key).First().Value = item.Value;
                    }
                    catch (Exception ex) { Console.WriteLine(ex); }
                }
            }
            avatarConfig.Name = config.Name;
            avatarConfig.Id = config.Id;
            config.Parameters.ParameterChanged += ParamsChanged;
            string json = JsonConvert.SerializeObject(avatarConfig, Formatting.Indented);
            File.WriteAllText("activeAvatar.json", json);
            avatarConfig.StripParams().UpdateParams();
        }


        private static readonly Dictionary<string, DateTime> paramCooldowns = new();
        private static readonly TimeSpan cooldownDuration = TimeSpan.FromMinutes(1);

        private static void ParamsChanged(OscAvatarParameter parameter, ValueChangedEventArgs e)
        {
            var param = avatarConfig.parameters.FirstOrDefault(p => p.Name == parameter.Name);

            if (param == null)
                return;

            param.Value = e.NewValue;
            //  Console.WriteLine(parameter.Name);
            if (ShouldSuppress(param.Name) || e.NewValue is not bool)
            {
                return;
            }
            //avatarConfig.AddParamStore();
            paramCooldowns[param.Name] = DateTime.UtcNow;
            avatarConfig.UpdateParams();
        }

        private static bool ShouldSuppress(string paramName)
        {
            if (paramName.Contains("Outfits")) return false;
            if (!paramCooldowns.TryGetValue(paramName, out var lastUpdate))
                return false;

            return (DateTime.UtcNow - lastUpdate) < cooldownDuration;
        }
        internal static void OnChangeRequested(ChangeParam change)
        {
            try
            {
                if (avatarConfig is null)
                    return;
                var param = avatarConfig.parameters.FirstOrDefault(p => p.Name == change.name);
                if (param is null)
                {
                    return;
                }
                Console.WriteLine($"Param Has Been Updated. {param.Name} -> {change.value}");
                param.Value = change.value;
                switch (param.Input.Type.ToLower())
                {
                    case "bool":
                        if (bool.TryParse(change.value, out bool boolVal))
                            OscParameter.SendAvatarParameter(change.name, boolVal);
                        else
                            Console.WriteLine($"Invalid bool value: {change.value}");
                        break;

                    case "int":
                        if (int.TryParse(change.value, out int intVal))
                            OscParameter.SendAvatarParameter(change.name, intVal);
                        else
                            Console.WriteLine($"Invalid int value: {change.value}");
                        break;

                    case "float":
                        if (float.TryParse(change.value, out float floatVal))
                            OscParameter.SendAvatarParameter(change.name, floatVal);
                        else
                            Console.WriteLine($"Invalid float value: {change.value}");
                        break;

                    default:
                        Console.WriteLine($"Unsupported type: {param.Input.Type}");
                        break;
                }

                avatarConfig.UpdateParams();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        private static void OnExit()
        {

            WebSocketManager.Shutdown();
            Environment.Exit(0);
        }

    }
}
