﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Diablo3Hub.Commons;
using Diablo3Hub.Models;
using Newtonsoft.Json;
using System.IO;
using Windows.UI.Popups;
using Template10.Services.NetworkAvailableService;

namespace Diablo3Hub.Services
{
    public class ApiHelper
    {
        private static ApiHelper _instance;

        private IList<KeyValuePair<string, string>> _apiKeys
            = new List<KeyValuePair<string, string>>();

        /// <summary>
        ///     인스턴스
        /// </summary>
        public static ApiHelper Instance
        {
            get { return _instance = _instance ?? new ApiHelper(); }
        }

        /// <summary>
        ///     배틀넷 API Key https://dev.battle.net/io-docs
        /// </summary>
        public string ApiKey
        {
            get { return !_apiKeys.Any() ? null : _apiKeys.FirstOrDefault(p => p.Key == "Key").Value; }
        }

        public string ApiSecret
        {
            get { return !_apiKeys.Any() ? null : _apiKeys.FirstOrDefault(p => p.Key == "Secret").Value; }
        }
        /// <summary>
        /// us, eu, tw, kr
        /// </summary>
        public string SelectedGameServer { get; set; }
        /// <summary>
        /// Locale ko_KR
        /// </summary>
        public string SelectedLocale { get; set; }

        /// <summary>
        ///     초기화, 반드시 사용전 호출해서 API Key를 설정하고 사용한다.
        /// </summary>
        /// <returns></returns>
        public async Task InitAsync()
        {
            _network = new NetworkAvailableService();

            var uri = new Uri("ms-appx:///ApiKeys.publishsettings");
            try
            {
                var apiFile = await StorageFile.GetFileFromApplicationUriAsync(uri);
                var content = await FileIO.ReadTextAsync(apiFile);
                string[] stringSeparators = { "\r\n" };
                var lines = content.Split(stringSeparators, StringSplitOptions.None);
                _apiKeys = (from kkk in lines
                            where kkk.Length > 0  //Apikey 파일 편집시 new line이 들어가는 경우, outofbound Exception이 발생하여 이 부분을 처리하였습니다.
                            let key = kkk.Split('=')
                            select new KeyValuePair<string, string>(key[0], key[1])).ToList();
            }
            catch (FileNotFoundException)
            {
                await new Windows.UI.Popups.MessageDialog("ApiKeys 파일을 찾지 못햇습니다. \n다시 확인해 주세요.").ShowAsync();
                return;
            }
            catch (Exception)
            {
                await new Windows.UI.Popups.MessageDialog("ApiKeys 파일을 읽는 중에 문제가 발생하였습니다. \n 다시 확인해 주세요.").ShowAsync();
                return;
            }
            
            SelectedGameServer = GameConfigs.ServerKR;
            SelectedLocale = GameConfigs.LocaleKR;
        }

        private INetworkAvailableService _network;

        /// <summary>
        /// Get CareerProfile 
        /// </summary>
        /// <param name="battleTag"></param>
        /// <returns></returns>
        public async Task<CareerProfile> GetCareerProfileAsync(string battleTag)
        {
            if (await _network.IsInternetAvailable() == false)
            {
                await CommonStaticHelper.ShowMessageBoxAsync("인터넷 연결이 필요합니다. 잠시후 다시 시도해 주시기 바랍니다.");
                return null;
            }

            var url = $"https://{SelectedGameServer}.api.battle.net/d3/profile/{battleTag.Replace("#","-")}/?locale={SelectedLocale}&apikey={ApiKey}";

            var cache = await DBHelper.Instance.ApiCacheTable()
                .Where(p => p.Url == url)
                .FirstOrDefaultAsync();
            if (cache != null)
            {
                var result = JsonConvert.DeserializeObject<CareerProfile>(cache.Content);
                return result;
            }

            using (var hc = new HttpClient())
            {
                var content = await hc.GetStringAsync(url);
                var jsonResult = JsonConvert.DeserializeObject<CareerProfile>(content);

                var item = new ApiCacheData
                {
                    Url = url,
                    Content = content,
                    CreateDT = DateTime.Now
                };
                var result = await DBHelper.Instance.InsertAsync(item);
                return jsonResult;
            }
        }
        /// <summary>
        /// 히어로 프로파일
        /// </summary>
        /// <param name="battleTag"></param>
        /// <param name="heroId"></param>
        /// <returns></returns>
        public async Task<HeroProfile> GetHeroProfileAsync(string battleTag, string heroId)
        {
            if (await _network.IsInternetAvailable() == false)
            {
                await CommonStaticHelper.ShowMessageBoxAsync("인터넷 연결이 필요합니다. 잠시후 다시 시도해 주시기 바랍니다.");
                return null;
            }

            using (var hc = new HttpClient())
            {
                var url = $"https://{SelectedGameServer}.api.battle.net/d3/profile/{battleTag.Replace("#", "-")}/hero/{heroId}?locale={SelectedLocale}&apikey={ApiKey}";
                var content = await hc.GetStringAsync(url);
                var jsonResult = JsonConvert.DeserializeObject<HeroProfile>(content);
                return jsonResult;
            }
        }
        /// <summary>
        /// 아이템 정보 조회
        /// </summary>
        /// <param name="itemCode"></param>
        /// <returns></returns>
        public async Task<ItemDetail> GetItemDetailAsync(string itemCode)
        {
            if (await _network.IsInternetAvailable() == false)
            {
                await CommonStaticHelper.ShowMessageBoxAsync("인터넷 연결이 필요합니다. 잠시후 다시 시도해 주시기 바랍니다.");
                return null;
            }
            var parts = itemCode.Split('/');
            if (parts.Length != 2 || parts.First() != "item") return null;

            using (var hc = new HttpClient())
            {
                var url = $"https://{SelectedGameServer}.api.battle.net/d3/data/item/{parts.Last()}?locale={SelectedLocale}&apikey={ApiKey}";
                var content = await hc.GetStringAsync(url);
                var jsonResult = JsonConvert.DeserializeObject<ItemDetail>(content);
                return jsonResult;
            }
        }
    }
}