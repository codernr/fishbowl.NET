using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fishbowl.Net.Server.Data;
using Fishbowl.Net.Server.Hubs;
using Fishbowl.Net.Shared;
using Fishbowl.Net.Shared.Data;
using Fishbowl.Net.Shared.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace Fishbowl.Net.Server.Services
{
    public class GameService
    {
        public AsyncGame Game { get; } = new();

        private readonly IHubContext<GameHub, IClient> hubContext;

        private readonly Map<string, Player> players = new();

        private readonly List<string> connections = new();

        private TaskCompletionSource<DateTimeOffset> inputAction = new();

        private int? teamCount;

        private IEnumerable<string>? roundTypes;

        private Task? gameLoop;

        private IEnumerable<string> RoundTypes => this.roundTypes ??
            throw new InvalidOperationException("Invalid game state: RoundTypes are not defined");

        private int TeamCount => this.teamCount ??
            throw new InvalidOperationException("Invalid game state: TeamCount is not defined");

        public GameService(IHubContext<GameHub, IClient> hubContext)
        {
            this.hubContext = hubContext;
            this.SetEventHandlers();
            this.Game.Run();
        }

        public void RegisterConnection(string connectionId)
        {
            if (!this.connections.Contains(connectionId))
            {
                this.connections.Add(connectionId);
            }
        }

        public void RemoveConnection(string connectionId)
        {
            if (this.players.ContainsKey(connectionId))
            {
                this.players.Remove(connectionId);
            }

            this.connections.Remove(connectionId);
        }

        public void AddPlayer(string connectionId, Player player)
        {
            this.players.Add(connectionId, player);

            this.Game.AddPlayer(player);

            if (this.players.Count == this.connections.Count) this.Game.PlayersSet();
        }

        private void SetEventHandlers()
        {
            this.Game.WaitingForTeamCount += this.WaitingForTeamCount;
            this.Game.WaitingForRoundTypes += this.WaitingForRoundTypes;
            this.Game.GameStarted += this.GameStarted;
            this.Game.GameFinished += this.GameFinished;
            this.Game.RoundStarted += this.RoundStarted;
            this.Game.RoundFinished += this.RoundFinished;
            this.Game.PeriodSetup += this.PeriodSetup;
            this.Game.PeriodStarted += this.PeriodStarted;
            this.Game.PeriodFinished += this.PeriodFinished;
            this.Game.ScoreAdded += this.ScoreAdded;
            this.Game.WordSetup += this.WordSetup;
        }

        private async void WaitingForTeamCount() =>
            await this.hubContext.Clients.Clients(this.connections.First()).DefineTeamCount();

        private async void WaitingForRoundTypes() =>
            await this.hubContext.Clients.Clients(this.connections.First()).DefineRoundTypes();

        private async void GameStarted(Game game) =>
            await this.hubContext.Clients.All.ReceiveGameStarted(game);

        private async void GameFinished(Game game) =>
            await this.hubContext.Clients.All.ReceiveGameFinished(game);

        private async void RoundStarted(Round round) =>
            await this.hubContext.Clients.All.ReceiveRoundStarted(round);

        private async void RoundFinished(Round round) =>
            await this.hubContext.Clients.All.ReceiveRoundFinished(round);

        private async void PeriodSetup(Period period) =>
            await this.hubContext.Clients.All.ReceivePeriodSetup(period);

        private async void PeriodStarted(Period period) =>
            await this.hubContext.Clients.All.ReceivePeriodStarted(period);

        private async void PeriodFinished(Period period) =>
            await this.hubContext.Clients.All.ReceivePeriodFinished(period);

        private async void ScoreAdded(Score score) =>
            await this.hubContext.Clients.All.ReceiveScoreAdded(score);

        private async void WordSetup(Player player, Word word) =>
            await this.hubContext.Clients.Clients(this.players[player]).ReceiveWordSetup(word);
    }
}