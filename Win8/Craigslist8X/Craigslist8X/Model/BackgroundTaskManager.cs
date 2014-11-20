using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace WB.Craigslist8X.Model
{
    static class BackgroundTaskManager
    {
        public static async Task RegisterAccess()
        {
            Unregister();

            try
            {
                var result = await BackgroundExecutionManager.RequestAccessAsync();

                switch (result)
                {
                    case BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity:
                    case BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity:
                        RegisterLockScreenTask();
                        break;
                    case BackgroundAccessStatus.Denied:
                    case BackgroundAccessStatus.Unspecified:
                    default:
                        RegisterMaintenanceTask();
                        break;
                }
            }
            catch
            {
                // If the user has already accepted lock screen access, an exception will be thrown. This is a bug in 
                // the API.
                RegisterLockScreenTask();
            }
        }

        static void Unregister()
        {
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == MaintenanceTaskName || task.Value.Name == LockScreenTaskName)
                    task.Value.Unregister(true);
            }
        }

        static void RegisterMaintenanceTask()
        {
            BackgroundTaskBuilder builder = new BackgroundTaskBuilder();
            builder.Name = MaintenanceTaskName;
            builder.TaskEntryPoint = TaskEntryPoint;
            builder.SetTrigger(new MaintenanceTrigger(freshnessTime: 15, oneShot: false));
            var condition = new SystemCondition(SystemConditionType.InternetAvailable);
            builder.AddCondition(condition);
            var x = builder.Register();
        }

        static void RegisterLockScreenTask()
        {
            BackgroundTaskBuilder builder = new BackgroundTaskBuilder();
            builder.Name = LockScreenTaskName;
            builder.TaskEntryPoint = TaskEntryPoint;
            builder.SetTrigger(new TimeTrigger(freshnessTime: 15, oneShot: false));
            var condition = new SystemCondition(SystemConditionType.InternetAvailable);
            builder.AddCondition(condition);
            var x = builder.Register();
        }

        const string LockScreenTaskName = "Lock Screen Search Agent Task";
        const string MaintenanceTaskName = "Maintenance Search Agent Task";
        const string TaskEntryPoint = "Craigslist8XTasks.SearchAgentTask";
    }
}
