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

    //private SpotifyControl activeSpotifyControl;
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
        string clientId = ev.ClientId.ToString();
        if(activeSpotifyAccount != null && activeSpotifyAccount.getClient() == clientId)
        {
            stopSpotify();
            info("User " + ev.ClientId.Value + " ("+ clientId + ") disconnected");
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
            LoginStepTwo(account, invoker);
        }
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
    public void LoginStepTwo(SpotifyAccount account, ClientCall invoker) {
        //get Email
        string information = "";
        information += "Now your Spotify-E-Mail.";
        Ts3Client.SendMessage(information, invoker.ClientId.Value);

        async Task handler(object sender, TextMessage textMessage)
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

        async Task handler(object sender, TextMessage textMessage)
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
        } else {
            Ts3Client.SendMessage("Everything seems right.", invoker.ClientId.Value);
            newInstance.stopProcess();
        }

        account.tsClientUniqueId = invoker.ClientUid.Value;

        spotifyPluginConfig.accountList.Add(account);

        saveConfig();        
    }
    #endregion loginSteps
    #region spotifyControl commands (search, next, song, changeSong usw)
    [Command("spotify check")]
    public void CommandSpotifyCheck(ClientCall invoker)
    {
        if (spotifyPluginConfig.librespotPath.Length <= 0) informChannel(Ts3Client, "librespot_path missing");
        if (!spotifyPluginConfig.librespotExists()) informChannel(Ts3Client, "librespot file missing");

        if (spotifyPluginConfig.deviceName.Length <= 0) informChannel(Ts3Client, "librespot_device_name empty");
        if (spotifyPluginConfig.getPipeName().Length <= 0) informChannel(Ts3Client, "librespot_pipeline_name empty");
       
        if (spotifyInstance == null)
            informChannel(Ts3Client, "spotifyInstance is null");
        else
        {
            informChannel(Ts3Client, "spotifyInstance " + (spotifyInstance.connected ? "has " : "not ") + "connected");
            informChannel(Ts3Client, "spotifyInstance " + (spotifyInstance.started ? "has " : "not ") + "started");
        }

        informChannel(Ts3Client, "producer is " + ((producer == null) ? "null" : "here"));
    }

    [Command("spotify stop")]
    public void commandSpotifyStop(ClientCall invoker)
    {
         stopSpotify();
    }
    #endregion

    public bool startProducer(IVoiceTarget targetManager)
    {

        if (OperatingSystem.IsWindows())
        {
            informChannel(Ts3Client, "Windows Version");


            // Winwdows currently not working 
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

        tries = 0;
        while (producer == null && tries++ < 10)
        {
            informChannel(Ts3Client, "Waiting for NamedPipe");
            Thread.Sleep(1000);
        }

        if (producer == null) return false;

        producer.start();

        return true;
    }
    [Command("spotify play")]
	public string commandSpotifyPlay(ClientCall invoker, IVoiceTarget targetManager)
	{
		if (!spotifyPluginConfig.librespotExists()) return "Please check Librespot-Path";

        SpotifyAccount account = spotifyPluginConfig.getAccount(invoker.ClientUid.Value);

        if (!account.Exists())
        {
            return "Spotify-Account not found!";
        }

        activeSpotifyAccount = account;
        
        if (spotifyInstance == null)
        {
            bool producerStarted = startProducer(targetManager);

            if(!producerStarted)
            {
                return "Librespot error.";
            }

            activeSpotifyAccount.setClient(invoker.ClientId.ToString());

            return "Start your Music in Spotify-App";
        }  

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



    private Task playBackBeforeRessourceStop(object sender, SongEndEventArgs e)
    {
        stopSpotify();

        return Task.CompletedTask;
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

            args += " -c \"" + Path.Combine(file.DirectoryName, Regex.Replace(account.tsClientUniqueId, "[^a-zA-Z0-9]", "")) + "\"";
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
            try
            {


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
            catch(Exception ex)
            {
                
            }

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
