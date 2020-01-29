﻿// Copyright 2017-2020 The NATS Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NATS.Client.Internals
{
    /// <summary>
    /// Handles in-flight requests when using the default (i.e. not old) request/reply behavior
    /// </summary>
    internal sealed class InFlightRequest : IDisposable
    {
        private readonly Action<string> _onCompleted;
        private readonly CancellationTokenSource _tokenSource;
        private readonly CancellationTokenRegistration _tokenRegistration;

        public string Id { get; }
        public CancellationToken Token { get; }
        public TaskCompletionSource<Msg> Waiter { get; } = new TaskCompletionSource<Msg>();
        private readonly CancellationToken _clientProvidedToken;

        /// <summary>
        /// Initializes a new instance of <see cref="InFlightRequest"/>
        /// </summary>
        /// <param name="id"></param>
        /// <param name="token"></param>
        /// <param name="timeout"></param>
        /// <param name="onCompleted"></param>
        internal InFlightRequest(string id, CancellationToken token, int timeout, Action<string> onCompleted)
        {
            _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
            _clientProvidedToken = token;
            Id = id;

            if (timeout > 0 && token == default)
            {
                _tokenSource = new CancellationTokenSource();
                Token = _tokenSource.Token;
            }
            else if (timeout > 0)
            {
                _tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                Token = _tokenSource.Token;
            } 
            else
            {
                Token = token;
            }

            _tokenRegistration = Token.Register((req) =>
            {
                var request = req as InFlightRequest;

                if (request._clientProvidedToken.IsCancellationRequested || timeout < 1)
                    request.Waiter.TrySetCanceled();

                request.Waiter.TrySetException(new NATSTimeoutException());
            }, this);

            if (timeout > 0)
                _tokenSource.CancelAfter(timeout);
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="InFlightRequest"/>
        /// class and invokes the <c>onCompleted</c> delegate.
        /// </summary>
        public void Dispose()
        {
            _tokenRegistration.Dispose();
            _tokenSource?.Dispose();
            _onCompleted.Invoke(Id);
        }
    }
}
