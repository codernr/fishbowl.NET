using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Fishbowl.Net.Client.Shared.Components
{
    public partial class StateManager
    {
        private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(300);

        [Parameter]
        public Action<object>? TransitionStarted { get; set; }

        private DynamicComponent Component => this.component ?? throw new InvalidOperationException();

        private DynamicComponent? component; 

        private Type? type;

        private bool show = false;

        private Task transition = Task.CompletedTask;

        private T Instance<T>() where T : class =>
            this.Component.Instance as T ?? throw new InvalidOperationException();

        public void SetParameters<T>(Action<T> setParameters) where T : class =>
            setParameters(this.Instance<T>());

        public Task SetStateAsync<T>(Action<T>? setParameters = null, TimeSpan delay = default) where T : class
        {
            this.transition = this.transition
                .ContinueWith(_ => this.TransitionAsync<T>(setParameters ?? (_ => {}), delay))
                .Unwrap();

            return this.transition;
        }

        private async Task TransitionAsync<T>(Action<T> setParameters, TimeSpan delay = default) where T : class
        {
            await this.DisableAsync();

            this.type = typeof(T);

            this.StateHasChanged();
         
            this.TransitionStarted?.Invoke(this.Instance<T>());

            this.SetParameters<T>(setParameters);

            await this.EnableAsync(delay);
        }

        private async Task EnableAsync(TimeSpan delay)
        {
            await Task.Delay(100);

            this.show = true;

            this.StateHasChanged();

            await Task.Delay(TransitionDuration + delay);
        }

        private async Task DisableAsync()
        {
            if (this.type is null)
            {
                return;
            }

            this.show = false;

            this.StateHasChanged();

            await Task.Delay(TransitionDuration);
        }
    }
}