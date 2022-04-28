
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
    private string clientId;
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
    public List<SpotifyAccount> accountList { get; set; } = new List<SpotifyAccount>();



    public string getPipeName()
    {
        return pipelineName;
    }
	public bool librespotExists()
	{
		return File.Exists(librespotPath);
	}

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
