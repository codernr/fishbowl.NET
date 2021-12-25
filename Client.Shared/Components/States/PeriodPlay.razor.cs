using Fishbowl.Net.Client.Shared.Store;
using Fishbowl.Net.Shared.ViewModels;

namespace Fishbowl.Net.Client.Shared.Components.States
{
    public partial class PeriodPlay
    {
        public PeriodRunningViewModel Period => this.State.Value.Period;

        protected override string Title => this.Period.Round.Type;

        private void AddScore() =>
            this.Dispatcher.Dispatch(new AddScoreAction(this.State.Value.Word!));

        private void Revoke() => this.Dispatcher.Dispatch(new RevokeLastScoreAction());

        private void FinishPeriod() =>
            this.Dispatcher.Dispatch(new FinishPeriodAction());
    }
}