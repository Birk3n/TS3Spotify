
using TS3AudioBot.Config;
using TSLib.Messages;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Models;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Auth;
using System.Timers;
using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
public class SpotifyControl
{
    private bool initialized = false;

    private SpotifyWebAPI _spotify;
	private Device _device;
	private SpotifyPluginConfig _spotifyConfig;


	private static Assembly SpotifyAPIWeb;
	private static Assembly SpotifyAPIAuth;


    public bool shuffle = false;
    private string lastLoadedType = "";
    private string lastLoadedID = "";

	public Timer reauthTimer;

    private int maxItemsList = 15;
    public SpotifyControl(SpotifyPluginConfig config, ConfRoot rootConf)
	{
		if (initialized) return;
		_spotifyConfig = config;
        if(SpotifyAPIWeb == null)
        {
		    var dll = Path.GetFullPath(Path.Combine(rootConf.Plugins.Path.Value, "lib", "SpotifyAPI.Web.dll"));
		    SpotifyAPIWeb = Assembly.LoadFrom(dll);
        } 
        if(SpotifyAPIAuth == null)
        {
		    var dll2 = Path.GetFullPath(Path.Combine(rootConf.Plugins.Path.Value, "lib", "SpotifyAPI.Web.Auth.dll"));
		    SpotifyAPIAuth = Assembly.LoadFrom(dll2);
        }
       

    }
    public bool hasInit()
    {
        return initialized;
    }
	public void firstTimeLogin(Action<string> callback)
	{
        AuthorizationCodeAuth auth = getAuth();
        callback(auth.GetUri());
	}
    private AuthorizationCodeAuth getAuth()
    {
        AuthorizationCodeAuth auth = new AuthorizationCodeAuth(
            _spotifyConfig.clientId,
            _spotifyConfig.clientSecret,
            _spotifyConfig.returnUrl,
            _spotifyConfig.returnUrl,
            (Scope.UserModifyPlaybackState | Scope.UserReadPlaybackState | Scope.UserReadCurrentlyPlaying)
        );
        return auth;
    }
    public void setTimer( string codeToken )
    {
        if (reauthTimer != null || initialized == false) return;

        reauthTimer = new Timer(45 * 60 * 1000);

        reauthTimer.Elapsed += new ElapsedEventHandler((e, v) => {
            if (!initialized) return;
            refreshtoken(codeToken, false);
        });

        reauthTimer.AutoReset = true;
        reauthTimer.Enabled = true;

    }
	public async void logintoken(string codeToken, Action<bool, bool, string, string> callback = null)
	{
		initialized = false;
        if (_spotify == null) _spotify = new SpotifyWebAPI();

        AuthorizationCodeAuth auth = getAuth();

        Token token = await auth.ExchangeCode(codeToken);

        _spotify.AccessToken = token.AccessToken;
		_spotify.TokenType = token.TokenType;

        if (!token.HasError())
		{
			initialized = true;
        } 
        if(callback != null)
        {
		    callback(initialized, true, token.AccessToken, token.RefreshToken);
        }
	}
    public async Task _refreshtoken(string codeToken) {
        initialized = false;
        if (_spotify == null) _spotify = new SpotifyWebAPI();
        AuthorizationCodeAuth auth = getAuth();
        Token token = await auth.RefreshToken(codeToken);
        _spotify.AccessToken = token.AccessToken;
        _spotify.TokenType = token.TokenType;
        
        if (token.HasError())
        {
            Console.WriteLine(token.Error + " - " + token.ErrorDescription);
        } else
        {
            initialized = true;
        }
    }
    public async void refreshtoken(string codeToken, bool tellPeople, Action<bool, bool, string, string> callback = null)
    {
        initialized = false;
        if (_spotify == null) _spotify = new SpotifyWebAPI();
        AuthorizationCodeAuth auth = getAuth();
        Token token = await auth.RefreshToken(codeToken);

        _spotify.AccessToken = token.AccessToken;
        _spotify.TokenType = token.TokenType;
        
        if (!token.HasError())
        {
            initialized = true;
        } else
        {
            Console.WriteLine(token.Error + " - " + token.ErrorDescription);
        }

        if (callback != null)
        {
            callback(initialized, tellPeople, token.AccessToken, token.RefreshToken);
        }
    }
    public void searchAlbum( string content,  Action<string> callback)
    {
        if (!initialized) return;
        SearchItem spotifySearchItems = _spotify.SearchItems(content, SearchType.Album, 10);
        string ret = "\nAlbumlist - " + content + "\n";

        for(int i = 0; i< spotifySearchItems.Albums.Items.Count;i++)
        {
            var item = spotifySearchItems.Albums.Items[i];

            string tempSend = ret;

            string date = item.ReleaseDate;
            if (item.ReleaseDatePrecision != "year")
            {
                date = DateTime.Parse(item.ReleaseDate).Year.ToString();
            }

            tempSend += "" + item.Id + " | " + date + " | " + item.Name + "\n";
            if (TSLib.Commands.TsString.TokenLength(tempSend) > TSLib.Commands.TsConst.MaxSizeTextMessage) {
                callback(ret);
                tempSend = "";
            } 
            ret = tempSend;
        }
        if(ret != "") {
            callback(ret);
        }
    }
    public void searchPlaylist(string content, Action<string> callback)
    {
        if (!initialized) return;
        SearchItem spotifySearchItems = _spotify.SearchItems(content, SearchType.Playlist, 10);

        string ret = "Playlist - " + content + "\n";
        for (int i = 0; i < spotifySearchItems.Playlists.Items.Count; i++)
        {
            var item = spotifySearchItems.Playlists.Items[i];

            string tempSend = ret;
            tempSend += item.Id + " - " + item.Name + "\n";

            if (TSLib.Commands.TsString.TokenLength(tempSend) > TSLib.Commands.TsConst.MaxSizeTextMessage)
            {
                callback(ret);
                tempSend = "";
            }
            ret = tempSend;
        }
        if (ret != "")
        {
            callback(ret);
        }
    }
    private string getArtistsString(List<SimpleArtist> _artists)
    {
        String[] artists = new String[_artists.Count];
        for (int i = 0; i < _artists.Count; i++)
        {
            artists[i] = _artists[i].Name;
        }
        return string.Join(", ", artists);
    }
    public void getPlaylistInfo(Action<string> callback)
    {
        var currentPlaylist = _spotify.GetPlaylistTracks(lastLoadedID);
        if (currentPlaylist.HasError())
        {
            callback("Something went wrong GetPlaylistTracks");
            return;
        }
        FullTrack track = getCurrentSong();
        string ret = "Playing Playlist:";

        int count = currentPlaylist.Items.Count;
        for (int i = 0; i < count; i++)
        {
            PlaylistTrack item = currentPlaylist.Items[i];

            ret += "\n";

            if (track.Id == item.Track.Id)
            {
                ret += "* ";
            }

            ret += getArtistsString(item.Track.Artists) + " - " + item.Track.Name;

            if (count < maxItemsList && i + 1 == count)
            {
                callback(ret);
            }
            else if (i != 0 && i % maxItemsList == 0)
            {
                callback(ret);
                ret = "";
            }
        }
        
    }
    public void getAlbumInfo(Action<string> callback)
    {

        var currentAlbum = _spotify.GetAlbumTracks(lastLoadedID);
        if (currentAlbum.HasError())
        {
            callback("Something went wrong GetAlbumTracks");
            return;
        }
        FullTrack track = getCurrentSong();
        string ret = "Playing Album:";

        int count = currentAlbum.Items.Count;
        for (int i = 0; i < count; i++)
        {
            SimpleTrack item = currentAlbum.Items[i];

            ret += "\n";

            if(track.Id == item.Id)
            {
                ret += "* ";
            }

            ret += getArtistsString(item.Artists) + " - " + item.Name;
            
            if(count < maxItemsList && i+1 == count)
            {
                callback(ret);
            } else if (i != 0 && i % maxItemsList == 0)
            {
                callback(ret);
                ret = "";
            }
        }

    }
    public void getList(Action<string> callback)
    {
        if (!initialized) return;
        if(lastLoadedID.Length<=0 || lastLoadedType.Length <= 0)
        {
            return;
        }
        
        if (lastLoadedType == "album")
        {
            getAlbumInfo(callback);

        } else if(lastLoadedType == "playlist")
        {
            getPlaylistInfo(callback);
        } else
        {
            callback("Type not supported: " + lastLoadedType);
            return;
        }

    }
    public void nextTitle(Action<string> callback, int count)
    {
        if (!initialized) return;
        for(int i = 0; i< count; i++)
        {
            ErrorResponse resumePlaybackError = _spotify.SkipPlaybackToNext(_device.Id);
            if (resumePlaybackError.HasError())
            {
                callback("Something went wrong");
                return;
            }
        }
    }
    public void prevTitle(Action<string> callback)
    {
        if (!initialized) return;
        ErrorResponse resumePlaybackError = _spotify.SkipPlaybackToPrevious(_device.Id);
        if (resumePlaybackError.HasError())
        {
            callback("Something went wrong");
        }
    }
    public void setShuffle(bool shuffleMode, Action<string> callback)
    {
        if (!initialized) return;
        shuffle = shuffleMode;
        ErrorResponse resumePlaybackError = _spotify.SetShuffle(shuffle);
        if (resumePlaybackError.HasError())
        {
            callback("Something went wrong");
        }
        
    }
    private FullTrack getCurrentSong()
    {
        if (!initialized) return new FullTrack();

        PlaybackContext ret = _spotify.GetPlayingTrack();
        if (ret.HasError())
        {
            return new FullTrack();
        }
        return ret.Item;
    }
    public void currentSong(Action<string> callback)
    {
        if (!initialized) return;


        FullTrack track = getCurrentSong();

        if (track.Name.Length > 0 && track.Artists.Count > 0)
        {
            String[] artists = new String[track.Artists.Count];
            for (var a = 0; a < track.Artists.Count; a++)
            {
                artists[a] = track.Artists[a].Name;
            }
            callback(string.Join(", ", artists) + " - " + track.Name);
        }

    }
    public void stop()
    {
        if (!initialized) return;
        ErrorResponse ret = _spotify.PausePlayback();
    }
    public void resumePlayback()
    {
        if (!initialized) return;

        if (_device == null)
        {
            var devices = _spotify.GetDevices();
            if (devices.Devices == null)
            {
                initialized = false;
              
                return;
            }

            for (int i = 0; i < devices.Devices.Count; i++)
            {
                if (devices.Devices[i].Name == _spotifyConfig.deviceName)
                {
                    _device = devices.Devices[i];
                    break;
                }
            }
            if (_device == null)
            {
                initialized = false;
                return;
            }
        }
        
        ErrorResponse resumePlaybackError = _spotify.ResumePlayback(_device.Id, "", null, "");

    }

    private void changeTitle(string to)
    {
        if (!initialized) return;
        if (_device == null)
        {
            var devices = _spotify.GetDevices();
            if (devices.Devices == null)
            {
                initialized = false;
                //callback("No devices :(");
                return;
            }

            for (int i = 0; i < devices.Devices.Count; i++)
            {
                if (devices.Devices[i].Name == _spotifyConfig.deviceName)
                {
                    _device = devices.Devices[i];
                    break;
                }
            }
            if (_device == null)
            {
                initialized = false;
                //callback("Device not found :(");
                return;
            }
        }

        ErrorResponse resumePlaybackError = _spotify.ResumePlayback(_device.Id, to, null, "");

        if (resumePlaybackError.HasError())
        {
           //callback("Something went wrong");
        }
       
    }
    public void change(string type, string id)
	{
		if (!initialized) return;

        lastLoadedType = type;
        lastLoadedID = id;
        changeTitle("spotify:" + type + ":" + id);
    }
	public bool isAlbum(string id)
    {
        FullAlbum album = _spotify.GetAlbum(id);
        return !album.HasError();
    }
    public bool isPlaylist(string id)
    {
        FullPlaylist playlist = _spotify.GetPlaylist(id, "", ""); ;
        return !playlist.HasError();
    }
}
