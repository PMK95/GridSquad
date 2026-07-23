using System;
using System.Collections.Generic;

namespace GridSquad
{
    public enum GameFlowState
    {
        Booting,
        BaseReady,
        PreparingMission,
        LoadingStage,
        InitializingStage,
        StageRunning,
        BetweenStages,
        SettlingMission,
        ReturningToBase,
        Failed
    }

    public readonly struct StageLaunchRequest
    {
        public readonly MissionDefinition Mission;
        public readonly MissionStageDefinition Stage;
        public readonly ActiveMissionState MissionState;

        public StageLaunchRequest(
            MissionDefinition mission,
            MissionStageDefinition stage,
            ActiveMissionState missionState)
        {
            Mission = mission;
            Stage = stage;
            MissionState = missionState;
        }
    }

    public sealed class GameFlowCoordinator
    {
        private readonly GameSessionState session;
        private readonly GameContentCatalog catalog;
        private readonly MissionStateFactory missionStateFactory = new();
        private readonly MissionSettlementService settlementService = new();
        private readonly BaseUnitStatCalculator statCalculator;
        private MissionDefinition activeMissionDefinition;

        public GameFlowCoordinator(GameSessionState session, GameContentCatalog catalog)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            statCalculator = new BaseUnitStatCalculator(catalog);
            State = GameFlowState.Booting;
        }

        public event Action<GameFlowState> StateChanged;
        public GameFlowState State { get; private set; }
        public GameSessionState Session => session;
        public MissionDefinition ActiveMissionDefinition => activeMissionDefinition;

        public void EnterBase()
        {
            if (session.ActiveMission != null)
                throw new InvalidOperationException("진행 중인 임무가 있어 기지 대기 상태로 전환할 수 없습니다.");
            activeMissionDefinition = null;
            ChangeState(GameFlowState.BaseReady);
        }

        public bool TryStartMission(
            MissionDefinition mission,
            IReadOnlyList<string> selectedUnitIds,
            int randomSeed,
            out string failureReason)
        {
            if (State != GameFlowState.BaseReady)
                return Fail("기지 대기 상태에서만 임무를 시작할 수 있습니다.", out failureReason);
            if (mission == null)
                return Fail("출발할 임무가 없습니다.", out failureReason);
            try
            {
                ChangeState(GameFlowState.PreparingMission);
                ActiveMissionState missionState = missionStateFactory.Create(
                    session.BaseState,
                    mission,
                    selectedUnitIds,
                    statCalculator.GetMaximumHealth,
                    statCalculator.GetTraumaResistance,
                    randomSeed);
                session.StartMission(missionState);
                activeMissionDefinition = mission;
                failureReason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                ChangeState(GameFlowState.BaseReady);
                failureReason = exception.Message;
                return false;
            }
        }

        public StageLaunchRequest CreateNextStageLaunchRequest()
        {
            ActiveMissionState missionState = session.ActiveMission
                ?? throw new InvalidOperationException("진행 중인 임무가 없습니다.");
            if (activeMissionDefinition == null)
                activeMissionDefinition = catalog.GetRequiredMission(missionState.MissionId);
            if (!activeMissionDefinition.TryGetStage(
                    missionState.NextStageIndex,
                    out MissionStageDefinition stage))
            {
                throw new InvalidOperationException(
                    $"실행할 스테이지가 없습니다: {missionState.NextStageIndex}");
            }
            ChangeState(GameFlowState.LoadingStage);
            return new StageLaunchRequest(activeMissionDefinition, stage, missionState);
        }

        public void NotifyStageInitializationStarted()
            => ChangeState(GameFlowState.InitializingStage);

        public void NotifyStageStarted()
            => ChangeState(GameFlowState.StageRunning);

        public bool CompleteStage(out bool missionCompleted)
        {
            if (State != GameFlowState.StageRunning)
                throw new InvalidOperationException("진행 중인 스테이지만 완료할 수 있습니다.");
            ActiveMissionState mission = session.ActiveMission;
            mission.AdvanceStage();
            missionCompleted = mission.NextStageIndex >= activeMissionDefinition.Stages.Count;
            ChangeState(missionCompleted
                ? GameFlowState.SettlingMission
                : GameFlowState.BetweenStages);
            return missionCompleted;
        }

        public void NotifyStageDefeated()
            => ChangeState(GameFlowState.SettlingMission);

        public MissionSettlement SettleMission(MissionEndReason endReason)
        {
            if (session.ActiveMission == null)
                throw new InvalidOperationException("정산할 임무가 없습니다.");
            ChangeState(GameFlowState.SettlingMission);
            MissionSettlement settlement = settlementService.Apply(
                session.BaseState,
                session.ActiveMission,
                endReason,
                catalog.AftereffectRules);
            session.FinishMission();
            activeMissionDefinition = null;
            ChangeState(GameFlowState.ReturningToBase);
            return settlement;
        }

        public void NotifyReturnedToBase()
            => ChangeState(GameFlowState.BaseReady);

        public void NotifyFailure(string reason)
        {
            ChangeState(GameFlowState.Failed);
            UnityEngine.Debug.LogError($"[게임 흐름] {reason}");
        }

        private void ChangeState(GameFlowState newState)
        {
            if (State == newState)
                return;
            State = newState;
            StateChanged?.Invoke(State);
        }

        private static bool Fail(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }
    }
}
