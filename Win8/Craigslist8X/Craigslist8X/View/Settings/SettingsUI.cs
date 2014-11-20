using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.ApplicationSettings;
using Windows.UI.Popups;
using Windows.System;

using Callisto.Controls;

namespace WB.Craigslist8X.View
{
    public static class SettingsUI
    {
        public static void ShowSearchSettings()
        {
            MainPage.Instance.ToggleWebView(show: false);

            SettingsFlyout settings = new SettingsFlyout();
            settings.HeaderText = "Search";
            settings.Content = new SearchSettings();
            settings.Closed += (s, e) => { MainPage.Instance.ToggleWebView(show: true); };
            settings.IsOpen = true;
        }

        public static void SettingsUI_CommandsRequested(SettingsPane sender, SettingsPaneCommandsRequestedEventArgs args)
        {
            SettingsCommand about = new SettingsCommand(AboutSettings, AboutSettings, GetCommandHandler(AboutSettings));
            SettingsCommand general = new SettingsCommand(GeneralSettings, GeneralSettings, GetCommandHandler(GeneralSettings));
            SettingsCommand search = new SettingsCommand(SearchSettings, SearchSettings, GetCommandHandler(SearchSettings));
            SettingsCommand privacy = new SettingsCommand(PrivacySettings, PrivacySettings, GetCommandHandler(PrivacySettings));

            args.Request.ApplicationCommands.Add(about);
            args.Request.ApplicationCommands.Add(general);
            args.Request.ApplicationCommands.Add(search);
            args.Request.ApplicationCommands.Add(privacy);
        }

        private static UICommandInvokedHandler GetCommandHandler(string setting)
        {
            return (x) =>
            {
                SettingsFlyout settings = new SettingsFlyout();
                settings.HeaderText = x.Label;

                if (setting == AboutSettings)
                {
                    settings.Content = new AboutSettings();
                }
                else if (setting == GeneralSettings)
                {
                    settings.Content = new GeneralSettings();
                }
                else if (setting == SearchSettings)
                {
                    settings.Content = new SearchSettings();
                }
                else if (setting == PrivacySettings)
                {
                    var res = Launcher.LaunchUriAsync(new Uri(PrivacyPolicyUrl));
                    return;
                }

                MainPage.Instance.ToggleWebView(show: false);
                settings.Closed += (s, e) => { MainPage.Instance.ToggleWebView(show: true); };
                settings.IsOpen = true;
            };
        }

        const string AboutSettings = "About";
        const string GeneralSettings = "General";
        const string SearchSettings = "Search";
        const string PrivacySettings = "Privacy Policy";

        const string PrivacyPolicyUrl = "http://wbishop.azurewebsites.net/privacy.aspx";
    }
}
