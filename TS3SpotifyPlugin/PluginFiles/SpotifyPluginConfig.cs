
using Nett;
using System.IO;
using System;
using TS3AudioBot.Config;
using System.Collections.Generic;
using TS3AudioBot;

public class SpotifyAccount
{
    public string id { get; set; } = "";
    public string email { get; set; } = "";
    //public string password { get; set; } = "";
    public string username { get; set; } = "";
    public string code { get; set; } = "";

    private string clientId;
    //user wont see this
    public string accessToken { get; set; } = "";
    public string refreshToken{ get; set; } = "";

    public bool Exists()
    {
        if (this.id != "") return true;

        return false;
    }
    public void setClient(string id) {
        clientId = id;
    }
    public string getClient() {
        return clientId;
    }
}
public class SpotifyPluginConfig
{
	public string deviceName { get; set; } = "TS3AudioBot";
	public string pipelineName { get; set; } = "TS3AudioBotFifo";
	public string librespotPath { get; set; } = "librespot";
    public string clientId { get; set; } = "";
    public string clientSecret { get; set; } = "";

    public string returnUrl { get; set; } = "";
    public List<SpotifyAccount> accountList { get; set; } = new List<SpotifyAccount>();



    //private string rndPipename;

    /*public SpotifyPluginConfig()
    {
        rndPipename = GetUniqID();
    }*/
    public string getPipeName()
    {
        return pipelineName;
    }
	public bool librespotExists()
	{
		return File.Exists(librespotPath);
	}
    // public void resetToken()
    // {
    //     spotify_refresh_token = "";
    //     spotify_access_token = "";
    // }
	// public bool hasLibrespotCache()
	// {
	// 	if(this.librespot_cache_folder.Length > 0)
	// 	{
	// 		if (File.Exists(Path.Join(this.librespot_cache_folder, "credentials.json"))) return true;
	// 	}
	// 	return false;
	// }
	// public bool canControl()
	// {
	// 	if(spotify_client_secret.Length > 0 && spotify_client_id.Length > 0)
	// 	{
	// 		return true;
	// 	}
	// 	return false;
	// }
	// public bool hasToken()
	// {
    //     return spotify_access_token.Length > 0 && spotify_refresh_token.Length > 0;
	// }

    public SpotifyAccount getAccount(string clientUid)
    {
        for(int i = 0; i < accountList.Count; i++)
        {
            if (accountList[i].id == clientUid) return accountList[i];
        }

        return new SpotifyAccount();
    }
    public void removeAccount(SpotifyAccount account)
    {
        accountList.Remove(account);
    }
}
