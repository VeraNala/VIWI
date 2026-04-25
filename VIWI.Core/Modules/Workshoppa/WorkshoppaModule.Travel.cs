using ECommons.Automation;
using System;
using System.Linq;
using VIWI.Modules.Workshoppa.Windows;
using static VIWI.Core.VIWIContext;
using static VIWI.Modules.Workshoppa.WorkshoppaConfig;

namespace VIWI.Modules.Workshoppa;

internal sealed partial class WorkshoppaModule
{
    private enum WorkshopTravelState
    {
        None,
        WaitingForArrival,
        LockingOn,
        Approaching,
        StartingQueue,
        Complete
    }

    private WorkshopTravelState _workshopTravelState = WorkshopTravelState.None;
    private DateTime _nextWorkshopTravelStep = DateTime.MinValue;
    public void BeginWorkshopTravel()
    {
        if (!_configuration.TeleToWorkshop || !ClientState.IsLoggedIn)
            return;

        PluginLog.Information("[Workshoppa] Auto-buy complete. Traveling to workshop...");
        Chat.ExecuteCommand("/li ws");

        _workshopTravelState = WorkshopTravelState.WaitingForArrival;
        _nextWorkshopTravelStep = DateTime.Now.AddSeconds(2);
    }
    private void HandleWorkshopTravel()
    {
        if (_workshopTravelState == WorkshopTravelState.None)
            return;

        if (!ClientState.IsLoggedIn || DateTime.Now < _nextWorkshopTravelStep)
            return;

        switch (_workshopTravelState)
        {
            case WorkshopTravelState.WaitingForArrival:
                {
                    if (WorkshopTerritories.Contains(ClientState.TerritoryType))
                    {
                        PluginLog.Information($"[Workshoppa] Arrived in workshop territory {ClientState.TerritoryType}.");
                        _workshopTravelState = WorkshopTravelState.LockingOn;
                        _nextWorkshopTravelStep = DateTime.Now.AddMilliseconds(500);
                    }
                    else
                    {
                        _nextWorkshopTravelStep = DateTime.Now.AddSeconds(1);
                    }

                    break;
                }

            case WorkshopTravelState.LockingOn:
                {
                    if (WorkshoppaHelpers.Lockon())
                    {
                        PluginLog.Information("[Workshoppa] Locked onto fabrication station.");
                        _workshopTravelState = WorkshopTravelState.Approaching;
                    }

                    _nextWorkshopTravelStep = DateTime.Now.AddMilliseconds(500);
                    break;
                }

            case WorkshopTravelState.Approaching:
                {
                    WorkshoppaHelpers.Approach();

                    if (WorkshoppaHelpers.AutomoveOffStation())
                    {
                        PluginLog.Information("[Workshoppa] Reached fabrication station.");
                        _workshopTravelState = WorkshopTravelState.StartingQueue;
                    }

                    _nextWorkshopTravelStep = DateTime.Now.AddMilliseconds(250);
                    break;
                }

            case WorkshopTravelState.StartingQueue:
                {
                    PluginLog.Information("[Workshoppa] Starting leveling queue.");
                    _configuration.Mode = TurnInMode.Leveling;
                    CurrentStage = Stage.TakeItemFromQueue;
                    ResetLevelingRuntimeState();
                    _workshopTravelState = WorkshopTravelState.Complete;
                    _nextWorkshopTravelStep = DateTime.Now.AddMilliseconds(100);
                    break;
                }

            case WorkshopTravelState.Complete:
                {
                    PluginLog.Information("[Workshoppa] Travel sequence & handoff complete.");
                    _workshopTravelState = WorkshopTravelState.None;
                    break;
                }
        }
    }
}
