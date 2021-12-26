using System;
using System.Threading.Tasks;
using Fishbowl.Net.Client.Online.Services;
using Fishbowl.Net.Client.Shared.Store;
using Fishbowl.Net.Shared;
using Fishbowl.Net.Shared.ViewModels;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Fishbowl.Net.Client.Online.Store
{
    [FeatureState]
    public record ConnetionState
    {
        public HubConnectionState State { get; init; }
    }

    public record SetConnectionStateAction(HubConnectionState State);
    public record ReceiveSetupPlayerAction(GameSetupViewModel Setup);
    public record ReceiveWaitForOtherPlayersAction(PlayerViewModel Player);
    public record ReceiveSetTeamNameAction(TeamSetupViewModel TeamSetup);
    public record ReceiveGameStartedAction();
    public record ReceiveGameFinishedAction(GameSummaryViewModel Summary);
    public record ReceiveRoundStartedAction(RoundViewModel Round);
    public record ReceiveRoundFinishedAction(RoundSummaryViewModel Summary);
    public record ReceivePeriodSetupAction(PeriodSetupViewModel Setup);
    public record ReceivePeriodStartedAction(PeriodRunningViewModel Running);
    public record ReceivePeriodFinishedAction(PeriodSummaryViewModel Summary);
    public record ReceiveWordSetupAction(WordViewModel Word);
    public record ReceiveScoreAddedAction(ScoreViewModel Score);
    public record ReceiveLastScoreRevokedAction(ScoreViewModel Score);

    public record GameContextExistsAction(string Password);
    public record GameContextExistsResponseAction(bool Exists);
    public record CreateGameContextSuccessAction();

    public record StatusErrorAction(StatusCode Status);

    public class ConnectionEffects : IAsyncDisposable
    {
        private readonly NavigationManager navigationManager;

        private readonly ILogger<ConnectionEffects> logger;

        private HubConnection connection = default!;

        private IDispatcher dispatcher = default!;

        public ConnectionEffects(
            NavigationManager navigationManager,
            ILogger<ConnectionEffects> logger) =>
            (this.navigationManager, this.logger) =
            (navigationManager, logger);

        [EffectMethod(typeof(StoreInitializedAction))]
        public Task Initialize(IDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;

            this.connection = new HubConnectionBuilder()
                .WithUrl(this.navigationManager.ToAbsoluteUri("/game"))
                .WithAutomaticReconnect()
                .Build();

            this.connection.Reconnecting += Dispatch;
            this.connection.Reconnected += Dispatch;
            this.connection.Closed += Dispatch;

            this.SetHandlers();

            return this.StartAsync();

            Task Dispatch(object? _)
            {
                this.dispatcher.Dispatch(new SetConnectionStateAction(this.connection.State));
                return Task.CompletedTask;
            }
        }

        private void SetHandlers()
        {
            this
                .On<GameSetupViewModel>(nameof(IGameClient.ReceiveSetupPlayer), p => new ReceiveSetupPlayerAction(p))
                .On<ReceivePlayerCountAction>(nameof(IGameClient.ReceivePlayerCount))
                .On<PlayerViewModel>(nameof(IGameClient.ReceiveWaitForOtherPlayers), p => new ReceiveWaitForOtherPlayersAction(p))
                .On<TeamSetupViewModel>(nameof(IGameClient.ReceiveSetTeamName), p => new ReceiveSetTeamNameAction(p))
                .On<ReceiveWaitForTeamSetupAction>(nameof(IGameClient.ReceiveWaitForTeamSetup))
                .On<ReceiveTeamNameAction>(nameof(IGameClient.ReceiveTeamName))
                .On<ReceiveRestoreStateAction>(nameof(IGameClient.ReceiveRestoreState))
                .On<ReceiveGameAbortAction>(nameof(IGameClient.ReceiveGameAborted))
                .On(nameof(IGameClient.ReceiveGameStarted), () => new ReceiveGameStartedAction())
                .On<GameSummaryViewModel>(nameof(IGameClient.ReceiveGameFinished), p => new ReceiveGameFinishedAction(p))
                .On<RoundViewModel>(nameof(IGameClient.ReceiveRoundStarted), p => new ReceiveRoundStartedAction(p))
                .On<RoundSummaryViewModel>(nameof(IGameClient.ReceiveRoundFinished), p => new ReceiveRoundFinishedAction(p))
                .On<PeriodSetupViewModel>(nameof(IGameClient.ReceivePeriodSetup), p => new ReceivePeriodSetupAction(p))
                .On<PeriodRunningViewModel>(nameof(IGameClient.ReceivePeriodStarted), p => new ReceivePeriodStartedAction(p))
                .On<PeriodSummaryViewModel>(nameof(IGameClient.ReceivePeriodFinished), p => new ReceivePeriodFinishedAction(p))
                .On<WordViewModel>(nameof(IGameClient.ReceiveWordSetup), p => new ReceiveWordSetupAction(p))
                .On<ScoreViewModel>(nameof(IGameClient.ReceiveScoreAdded), p => new ReceiveScoreAddedAction(p))
                .On<ScoreViewModel>(nameof(IGameClient.ReceiveLastScoreRevoked), p => new ReceiveLastScoreRevokedAction(p));
        }

        private async Task StartAsync()
        {
            await this.connection.StartAsync();

            this.dispatcher.Dispatch(new SetConnectionStateAction(this.connection.State));
        }

        [EffectMethod]
        public async Task OnJoinGameContext(JoinGameContextAction action, IDispatcher dispatcher)
        {
            var response = await this.connection.InvokeAsync<StatusResponse>("JoinGameContext", action);

            if (response.Status == StatusCode.Ok) return;

            dispatcher.Dispatch(new StatusErrorAction(response.Status));
        }

        [EffectMethod]
        public async Task OnCreateGameContext(CreateGameContextAction action, IDispatcher dispatcher)
        {
            var response = await this.connection.InvokeAsync<StatusResponse>("CreateGameContext", action);

            if (response.Status == StatusCode.Ok)
            {
                dispatcher.Dispatch(new CreateGameContextSuccessAction());
                return;
            }

            dispatcher.Dispatch(new StatusErrorAction(response.Status));
        }

        [EffectMethod]
        public async Task OnAddPlayer(AddPlayerAction action, IDispatcher dispatcher)
        {
            await this.connection.InvokeAsync<StatusResponse>("AddPlayer", action);
        }

        [EffectMethod]
        public async Task OnSubmitTeamName(SubmitTeamNameAction action, IDispatcher dispatcher)
        {
            await this.connection.InvokeAsync<StatusResponse>("SubmitTeamName", action);
        }

        [EffectMethod]
        public async Task OnAddScore(AddScoreAction action, IDispatcher dispatcher)
        {
            await this.SendAsync("AddScore", action.Word);
            await this.SendAsync("NextWord");
        }

        [EffectMethod(typeof(RevokeLastScoreAction))]
        public Task OnRevokeLastScore(IDispatcher dispatcher) =>
            this.SendAsync("RevokeLastScore");

        [EffectMethod(typeof(FinishPeriodAction))]
        public Task OnFinishPeriod(IDispatcher dispatcher) =>
            this.SendAsync("FinishPeriod");

        private ConnectionEffects On(string methodName, Func<object> factory)
        {
            this.connection.On(methodName, Dispatch(factory));
            this.connection.On(methodName, () => this.logger.LogInformation("{MethodName}", methodName));
            return this;

            Func<Task> Dispatch(Func<object> factory) => () =>
            {
                this.dispatcher.Dispatch(factory());
                return Task.CompletedTask;
            };
        }

        private ConnectionEffects On<T>(string methodName, Func<T, object> factory)
        {
            this.connection.On<T>(methodName, (T param) => this.dispatcher.Dispatch(factory(param)));
            this.connection.On<T>(methodName, data => this.logger.LogInformation("{MethodName}: {Data}", methodName, data));
            return this;
        }

        private ConnectionEffects On<T>(string methodName)
        {
            this.connection.On<T>(methodName, (T param) => this.dispatcher.Dispatch(param));
            this.connection.On<T>(methodName, data => this.logger.LogInformation("{MethodName}: {Data}", methodName, data));
            return this;
        }

        private Task SendAsync(string methodName, object? arg = null)
        {
            this.logger.LogInformation("{MethodName}: {Data}", methodName, arg);
            return arg is null ? this.connection.SendAsync(methodName) : this.connection.SendAsync(methodName, arg);
        }

        public ValueTask DisposeAsync() => this.connection.DisposeAsync();
    }
}