using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

using TS3AudioBot;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.Plugins;
using TS3AudioBot.Config;


using TSLib.Audio;
using TSLib.Full;
using TSLib.Messages;


using Nett;

public class SpotifyCommand : IBotPlugin
{
    
    public PlayManager PlayManager { get; set; }
	public Ts3Client Ts3Client { get; set; }
    public Player player { get; set; }
    public ConfBot botConf { get; set; }
	public ConfRoot rootConf { get; set; }
    public TsFullClient fullClient { get; set; }


    private SpotifyStreamAudioProducer producer;
	private SpotifyPluginConfig spotifyPluginConfig;
    private SpotifyInstance spotifyInstance;

    private SpotifyControl activeSpotifyControl;
    private SpotifyAccount activeSpotifyAccount;

    private static readonly Object _lock = new Object();

    public static NLog.Logger Log = NLog.LogManager.GetLogger($"TS3AudioBot.Plugins.{PluginInfo.ShortName}");
    public void spotifycommand(){}
    public void info(string message) {
        Log.Info("* " + message);
        Ts3Client.SendChannelMessage("* Spotify: " + message);
    }
    public void Initialize()
	{
        this.info("Spotify initialized");
        //Load or create config
        if (botConf.TomlObject.TryGetValue("spotify", out var tempBotSpotifyTomlObject))
		{
		    spotifyPluginConfig = tempBotSpotifyTomlObject.Get<SpotifyPluginConfig>();
            this.info("Config loaded");
            
        }
		else
		{
	        spotifyPluginConfig = new SpotifyPluginConfig();
            saveConfig();
            
            this.info("Config created - Please edit your Bot-Config and reload plugin");
		}
        fullClient.OnEachClientLeftView += StopSpotifyOnLeave;
        PlayManager.ResourceStopped += playBackBeforeRessourceStop;
    }
    public void StopSpotifyOnLeave(object sender, ClientLeftView ev)
    {
        string id = ev.ClientId.ToString();
        if(activeSpotifyAccount != null && activeSpotifyAccount.getClient() == id)
        {
            stopSpotify();
            info("User " + ev.ClientId.Value + " ("+id+") disconnected");
        }
    }

    #region account
    [Command("spotify account show")]
    public void CommandSpotifyAccountShow(ClientCall invoker) => CommandSpotifyAccount(invoker);

    
    [Command("spotify account")]
    public void CommandSpotifyAccount(ClientCall invoker)
    {
        SpotifyAccount account = spotifyPluginConfig.getAccount(invoker.ClientUid.Value);
        string accountInformation = "";
        if(account.Exists())
        {
            accountInformation += "Your Accountinformation:\n\n";
            accountInformation += "Your E-Mail: " + account.email + "\n";
            accountInformation += "Your Code: " + account.code + "\n";

        } else
        {
            accountInformation += "Yout dont have an Spotify-Account stored. Type !spotify account add";
        }
        Ts3Client.SendMessage(accountInformation, invoker.ClientId.Value);
    }
    [Command("spotify account add")]
    public void CommandSpotifyAccountAdd(ClientCall invoker)
    {
        SpotifyAccount account = spotifyPluginConfig.getAccount(invoker.ClientUid.Value);

        if (account.Exists())
        {
            CommandSpotifyAccount(invoker);
        }
        else
        {
            string information = "Follow instructions";
            Ts3Client.SendMessage(information, invoker.ClientId.Value);
            LoginStepOne(account, invoker);
        }
    }
    [Command("spotify account edit")]
    public string CommandSpotifyAccountEdit(ClientCall invoker, string property)
    {
        SpotifyAccount account = spotifyPluginConfig.getAccount(invoker.ClientUid.Value);

        if(!account.Exists()) return "No account found.";

        property = property.ToLower();

        if (property == "pass" || property == "password" || property == "passwort") {
            LoginStepThree(account, invoker); //follow it, we dont store passwords
        } else if (property == "auth") {
            LoginStepOne(account, invoker);
        } else {
            return "Not found.";
        }

        return "";
        
    }
    [Command("spotify account delete")]
    public string CommandSpotifyAccountDelete(ClientCall invoker)
    {
        SpotifyAccount account = spotifyPluginConfig.getAccount(invoker.ClientUid.Value);

        if (account.Exists())
        {
            spotifyPluginConfig.removeAccount(account);
            saveConfig();
            return "Done.";
        }
        else
        {
            return "No Account found.";
        }

    }
    #endregion
    #region loginSteps
    public void LoginStepOne(SpotifyAccount account, ClientCall invoker) {
        //get Auth-Key
        account.id = invoker.ClientUid.Value;
        var newControl = new SpotifyControl(spotifyPluginConfig, rootConf);
        newControl.firstTimeLogin((link) =>
        {
            string information = "";
            information += "Grant rights and place the code from GET-Param into this chat.";
            Ts3Client.SendMessage(information, invoker.ClientId.Value);
            Ts3Client.SendMessage(link, invoker.ClientId.Value);

            void handler(object sender, TextMessage textMessage)
            {
                if (textMessage.InvokerId == invoker.ClientId) {
                    account.code = textMessage.Message.ToString();
                    Ts3Client.OnMessageReceived -= handler;
                    LoginStepTwo(account, invoker);
                }
            };

            Ts3Client.OnMessageReceived += handler;
        });
    }
    public void LoginStepTwo(SpotifyAccount account, ClientCall invoker) {
        //get Email
        string information = "";
        information += "Now your Spotify-E-Mail.";
        Ts3Client.SendMessage(information, invoker.ClientId.Value);

        void handler(object sender, TextMessage textMessage)
        {
            if (textMessage.InvokerId == invoker.ClientId) {
                string mail = textMessage.Message.ToString();
                Match m = Regex.Match(mail, @"\[URL=.*\](.*)\[\/URL\]");
                if (m.Success)
                {
                    account.email = m.Groups[m.Groups.Count - 1].Value;
                } else
                {
                    account.email = mail;
                }

                Ts3Client.OnMessageReceived -= handler;
                LoginStepThree(account, invoker);
            }
        };

        Ts3Client.OnMessageReceived += handler;
    }
    public void LoginStepThree(SpotifyAccount account, ClientCall invoker) {
        //get password
        string information = "";
        information += "Now your Spotify-Password.";
        Ts3Client.SendMessage(information, invoker.ClientId.Value);

        void handler(object sender, TextMessage textMessage)
        {
            if (textMessage.InvokerId == invoker.ClientId) {
                Ts3Client.OnMessageReceived -= handler;
                LoginStepFour(account, invoker, textMessage.Message.ToString());
            }
        };

        Ts3Client.OnMessageReceived += handler;
    }
    public void LoginStepFour(SpotifyAccount account, ClientCall invoker, string password) {
        //Start Librespot and check Password
        string information = "";
        information += "Lets check it quickly.";
        Ts3Client.SendMessage(information, invoker.ClientId.Value);

        SpotifyInstance newInstance = new SpotifyInstance(spotifyPluginConfig, account);
        newInstance.useLogin(account.email, password);
        newInstance.startProcess();

        int tries = 0;

        while(!newInstance.connected && tries++ < 4)
        {
            Thread.Sleep(500);
        }

        if(!newInstance.connected || newInstance.hasExited())
        {
            Ts3Client.SendMessage("Credentials not right.", invoker.ClientId.Value);
            return;
        }


        SpotifyControl newControl = new SpotifyControl(spotifyPluginConfig, rootConf);

        newControl.logintoken(account.code, (bool success, bool tell, string accessToken, string refreshToken) =>
        {
            if (success)
            {
                account.refreshToken = refreshToken;
                account.accessToken = accessToken;

                spotifyPluginConfig.accountList.Add(account);
                saveConfig();

                Ts3Client.SendMessage("You can use Spotify now.", invoker.ClientId.Value);
            }
            else
            {
                Ts3Client.SendMessage("Something went wrong check your Auth-Token.", invoker.ClientId.Value);
            }
        });

    }
    #endregion
    #region spotifyControl commands (search, next, song, changeSong usw)
    [Command("spotify search")]
    public string CommandSpotifySearchDefault()
    {
        string ret = "";
        ret += "\n!spotify search \"string\" => searching for Albums";
        ret += "\n!spotify search playlist \"string\" => searching for playlist";
        ret += "\n!spotify search pl \"string\" => searching for playlist";
        return ret;
    }
    [Command("spotify search")]
    public void CommandSpotifySearch(ClientCall invoker, string content)
    {
        if (!checkControlAvailable(invoker)) return;

        lock(_lock)
        {
            activeSpotifyControl.searchAlbum(content, (string ret) => {
                informChannel(Ts3Client, ret);
            });
            activeSpotifyControl.searchPlaylist(content, (string ret) => {
                informChannel(Ts3Client, ret);
            });
        }
    }
    [Command("spotify search")]
    public void CommandSpotifySearch(ClientCall invoker, string type, string content)
    {
        if (!checkControlAvailable(invoker)) return;

        if (type == "playlist" || type == "pl")
        {
            lock (_lock)
            {
                activeSpotifyControl.searchPlaylist(content, (string ret) =>
                {
                    informChannel(Ts3Client, ret);
                });
            }
        } else if (type == "album" )
        {
            lock (_lock)
            {
                activeSpotifyControl.searchAlbum(content, (string ret) =>
                {
                    informChannel(Ts3Client, ret);
                });
            }
        }
        else
        {
            informChannel(Ts3Client, "No search command found");
        }

    }
    
   
    [Command("spotify list")]
    //[Usage()]
    public void CommandSpotifyList(ClientCall invoker)
    {
        if (!checkControlAvailable(invoker)) return;
        lock (_lock)
        {
            activeSpotifyControl.getList((string ret) => informChannel(Ts3Client, ret));
        }
    }
    [Command("spotify next")]
    public void CommandSpotifyNextSingle(ClientCall invokert) => CommandSpotifyNext(invokert, 1);

    [Command("spotify next")]
    public void CommandSpotifyNext(ClientCall invoker, int count)
    {
        if (!checkControlAvailable(invoker)) return;
        lock (_lock)
        {
            activeSpotifyControl.nextTitle((string ret) => informChannel(Ts3Client, ret), count);
        }
    }
    [Command("spotify prev")]
    public void CommandSpotifyPrev(ClientCall invoker)
    {
        if (!checkControlAvailable(invoker)) return;
        lock (_lock)
        {
            activeSpotifyControl.prevTitle((string ret) => informChannel(Ts3Client, ret));
        }
    }
    [Command("spotify shuffle")]
    public void CommandSpotifyShuffle(ClientCall invoker)
    {
        if (!checkControlAvailable(invoker)) return;
        informChannel(Ts3Client, "Shuffle is " + (activeSpotifyControl.shuffle ? "on" : "off") );
    }
    [Command("spotify shuffle")]
    public void CommandSpotifyShuffle(ClientCall invoker, string shuffleMode)
    {
        if (!checkControlAvailable(invoker)) return;
        lock (_lock)
        {
            activeSpotifyControl.setShuffle(shuffleMode == "on", (string ret) => informChannel(Ts3Client, ret));
        }
    }

    [Command("spotify song")]
    public void CommandSpotifySong(ClientCall invoker)
    {
        if (!checkControlAvailable(invoker)) return;
        lock (_lock)
        {
            activeSpotifyControl.currentSong((string ret) => informChannel(Ts3Client, ret));
        }
    }
    [Command("spotify check")]
    public void CommandSpotifyCheck(ClientCall invoker)
    {
        if (spotifyPluginConfig.librespotPath.Length <= 0) informChannel(Ts3Client, "librespot_path missing");
        if (!spotifyPluginConfig.librespotExists()) informChannel(Ts3Client, "librespot file missing");

        if (spotifyPluginConfig.deviceName.Length <= 0) informChannel(Ts3Client, "librespot_device_name empty");
        if (spotifyPluginConfig.getPipeName().Length <= 0) informChannel(Ts3Client, "librespot_pipeline_name empty");
       
        if (spotifyPluginConfig.clientId.Length <= 0) informChannel(Ts3Client, "spotify_client_id empty");
        if (spotifyPluginConfig.clientSecret.Length <= 0) informChannel(Ts3Client, "spotify_client_secret empty");

        if (spotifyInstance == null)
            informChannel(Ts3Client, "spotifyInstance is null");
        else
        {
            informChannel(Ts3Client, "spotifyInstance " + (spotifyInstance.connected ? "has " : "not ") + "connected");
            informChannel(Ts3Client, "spotifyInstance " + (spotifyInstance.started ? "has " : "not ") + "started");
        }

        informChannel(Ts3Client, "producer is " + ((producer == null) ? "null" : "here"));
    }
    [Command("spotify track")]
    public void commandSpotifyTrack(ClientCall invoker, string userString) => changeMusic(userString, "track");

    [Command("spotify album")]
    public void commandSpotifyAlbum(ClientCall invoker, string userString) => changeMusic(userString, "album");

    [Command("spotify playlist")]
    public void commandSpotifyPlaylist(ClientCall invoker, string userString) => changeMusic(userString, "playlist");

    private void changeMusic(string userString, string type = "")
    {
        lock (_lock)
        {
            if (type != "")
            {
                activeSpotifyControl.change(type.ToLower(), userString);
            }
            else if (activeSpotifyControl.isAlbum(userString))
            {
                activeSpotifyControl.change("album", userString);
            }
            else if (activeSpotifyControl.isPlaylist(userString))
            {
                activeSpotifyControl.change("playlist", userString);
            }
            else
            {
                informChannel(Ts3Client, "No Album/Playlist found");
            }
        }
    }

    [Command("spotify stop")]
    public void commandSpotifyStop(ClientCall invoker)
    {
        lock (_lock)
        {
            if (checkControlAvailable(invoker)) activeSpotifyControl.stop();
        }
        // stopSpotify();
    }
    #endregion

    private bool checkControlAvailable(ClientCall invoker)
    {
        if (activeSpotifyControl != null && activeSpotifyControl.hasInit()) return true;


        return _startControl(invoker);
    }
    public bool _startControl(ClientCall invoker) {
        if (activeSpotifyAccount != null) return false;
        SpotifyAccount account = spotifyPluginConfig.getAccount(invoker.ClientUid.Value);
        if(!account.Exists())
        {
            return false;
        }
        SpotifyControl tempControl = new SpotifyControl(spotifyPluginConfig, rootConf);

        tempControl._refreshtoken(account.refreshToken).Wait();

        if(!tempControl.hasInit()) {
            return false;
        }

        activeSpotifyAccount = account;
        activeSpotifyAccount.setClient(invoker.ClientId.ToString());


        activeSpotifyControl = tempControl;
        activeSpotifyControl.setTimer(activeSpotifyAccount.refreshToken);

        return true;
    }  
    public bool startProducer(IVoiceTarget targetManager)
    {

        if (OperatingSystem.IsWindows())
        {
            informChannel(Ts3Client, "Windows Version");

            Task.Factory.StartNew(() =>
            {
                //Starting a Task bc WaitForConnection blocks execution

                var stream = new NamedPipeServerStream(spotifyPluginConfig.getPipeName(), PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None);
                stream.WaitForConnection();
                producer = new SpotifyStreamAudioProducer(stream, player, rootConf);
            });


        }
        else
        {
            informChannel(Ts3Client, "Linux Version");
            var fifo = "/tmp/" + spotifyPluginConfig.getPipeName();
            if (!File.Exists(fifo))
            {
                var output = ("mkfifo " + fifo).Bash();
                Console.WriteLine(output);
            }
            FileStream s = new FileStream("/tmp/" + spotifyPluginConfig.getPipeName(), FileMode.Open);
            producer = new SpotifyStreamAudioProducer(s, player, rootConf);
        }

        spotifyInstance = new SpotifyInstance(spotifyPluginConfig, activeSpotifyAccount);
        spotifyInstance.startProcess();

        int tries = 0;

        while (!spotifyInstance.connected && tries++ < 4)
        {
            Thread.Sleep(500);
        }

        if (!spotifyInstance.connected || spotifyInstance.hasExited())
        {
            return false;
        }

        targetManager.SendMode = TargetSendMode.Voice;

        producer.start();
        return true;
    }
    [Command("spotify play")]
	public string commandSpotifyPlay(ClientCall invoker, IVoiceTarget targetManager, string id)
	{
		if (!spotifyPluginConfig.librespotExists()) return "Please check Librespot-Path";

        if (id == "") return "nothing given.";

        if(!checkControlAvailable(invoker)) {
            return "SpotifyControl error.";
        }

        if(spotifyInstance == null)
        {
            bool producerStarted = startProducer(targetManager);

            if(!producerStarted)
            {
                return "Librespot error.";
            }
        }  

        changeMusic(id);

        return "";
	}


    [Command("spotify testfile")]
	public string commandSpotifyTestfile(ClientCall invoker, IVoiceTarget targetManager)
	{
		if (producer == null)
		{
			FileStream s = new FileStream("testfile", FileMode.Open);
			producer = new SpotifyStreamAudioProducer(s, player, rootConf);
		}

		targetManager.SendMode = TargetSendMode.Voice;

		producer.startTestFile();

        informChannel(Ts3Client, "Mixed in :-)");

		return "asd";
	}
	[Command("spotify testsample")]
	public string commandSpotifyTestsample(ClientCall invoker, IVoiceTarget targetManager)
	{
		if(producer == null)
		{
			FileStream s = new FileStream("testfile", FileMode.Open);
			producer = new SpotifyStreamAudioProducer(s, player, rootConf);
		}

		targetManager.SendMode = TargetSendMode.Voice;

		producer.startTestSample();

		informChannel(Ts3Client, "Mixed in :-)");

		return "asd";
	}



    private void playBackBeforeRessourceStop(object sender, EventArgs e)
    {
        stopSpotify();
    }


    private void stopSpotify()
    {
        lock (_lock)
        {
            if (spotifyInstance != null && !spotifyInstance.hasExited())
            {
                spotifyInstance.stopProcess();
            }

            producer?.Dispose();
            producer = null;
            spotifyInstance = null;
            activeSpotifyControl = null;
            activeSpotifyAccount = null;
        }
    }
	public void informChannel(Ts3Client ts3Client, string message)
	{
		ts3Client.SendChannelMessage("* Spotify: " + message);
	}
    private void saveConfig()
    {
        botConf.TomlObject.Remove("spotify");
        botConf.TomlObject.Add("spotify", spotifyPluginConfig);
        botConf.SaveWhenExists();
    }
    public void Dispose()
	{
        stopSpotify();

        PlayManager.ResourceStopped -= playBackBeforeRessourceStop;
        fullClient.OnEachClientLeftView -= StopSpotifyOnLeave;

        if (OperatingSystem.IsLinux())
        {
            var output = ("rm " + spotifyPluginConfig.getPipeName()).Bash();
        }
    }

	private class SpotifyInstance
	{

		Process instance;
        SpotifyPluginConfig config;
        SpotifyAccount account;

		public bool connected = false;
        public bool started = false;
        public bool useLoginCredentials = false;

        string filePath;

		private string args = " -b 160 --enable-volume-normalisation --initial-volume 100 --backend pipe";

        private string defaultArgs = " -b 160 --enable-volume-normalisation --initial-volume 100 --backend pipe";

        public SpotifyInstance(SpotifyPluginConfig config, SpotifyAccount account)
		{
            this.config = config;
            this.account = account;
            resetArgs();

		}
        private void resetArgs()
        {
            useLoginCredentials = false;
            args = defaultArgs;
            args += " -n " + config.deviceName;

            var file =  new System.IO.FileInfo(config.librespotPath);
            filePath =  file.FullName;

            args += " -c \"" + Path.Combine(file.DirectoryName, Regex.Replace(account.id, "[^a-zA-Z0-9]", "")) + "\"";
            if (OperatingSystem.IsWindows())
            {
                args += " --device \"\\\\.\\pipe\\" + config.getPipeName() + "\"";
            }
            else
            {
                args += " --device /tmp/" + config.getPipeName();
            }
        }

        public bool hasExited()
        {
            if (instance != null) return instance.HasExited;

            return true;
        }

        public void useLogin(string email, string password)
		{
            resetArgs();
			useLoginCredentials = true;

            args += " -u " + email;
			args += " -p " + password;
		}

		public void stopProcess()
		{
            instance?.Kill();
			instance = null;
        }
		public void setConnected()
		{
			this.connected = true;
		}
		public void startProcess()
		{
            started = true;
            Console.WriteLine("Starting with {0}", args);
			instance = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = filePath,
					Arguments = args,
					RedirectStandardOutput = true,
					RedirectStandardInput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				},
				EnableRaisingEvents = true,
			};

			instance.ErrorDataReceived += (s, e) => outputData(e.Data);

			instance.Start();
			instance.BeginErrorReadLine();
		}
		private void outputData(string data)
		{
			if (String.IsNullOrEmpty(data)) return;

			if(data.Contains("Country"))
			{
				connected = true;
			}
            Log.Debug(data);
        }

    }

    public static class PluginInfo
    {
        public static readonly string ShortName;
        public static readonly string Name = "TS3Spotify";
        public static readonly string Description = "";
        public static readonly string Url = $"";
        public static readonly string Author = "me <myself@example.com>";
        public static readonly Version Version = System.Reflection.Assembly.GetCallingAssembly().GetName().Version;

        static PluginInfo()
        {
            ShortName = typeof(PluginInfo).Namespace;
            var name = System.Reflection.Assembly.GetCallingAssembly().GetName().Name;
            Name = string.IsNullOrEmpty(name) ? ShortName : name;
        }
    }
    public static class OperatingSystem
    {
        public static bool IsWindows() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static bool IsMacOS() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static bool IsLinux() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }
}
