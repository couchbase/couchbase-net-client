using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace Couchbase {

    public enum StaleMode { AllowStale, UpdateAfter, False }

    public enum OnErrorMode { Continue, Stop }

    public class ViewParamsBuilder {

        private readonly Dictionary<string, string> _viewParams;

        public ViewParamsBuilder() {
            _viewParams = new Dictionary<string, string>();
        }

        public void AddParam(string key, string value)
        {
            _viewParams.Add(key, value);
        }

        public void AddOptionalParam(string key, bool? value)
        {
            if (value != null && value.HasValue)
            {
                _viewParams.Add(key, value.Value ? "true" : "false");
            }
        }

        public void AddOptionalParam(string key, int? value)
        {
            if (value != null && value.HasValue)
            {
                _viewParams.Add(key, value.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void AddGreaterThanOneParam(string key, int? value)
        {
            if (value != null && value.HasValue && value.Value > 0)
            {
                _viewParams.Add(key, value.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void AddOptionalParam<T>(string key, T value) where T : IConvertible
        {
            if (value != null)
            {
                _viewParams.Add(key, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void AddStaleParam(StaleMode? stale)
        {
            if (stale != null)
            {
                switch (stale.Value)
                {
                    case StaleMode.AllowStale:
                        _viewParams.Add("stale", "ok");
                        break;
                    case StaleMode.UpdateAfter:
                        _viewParams.Add("stale", "update_after");
                        break;
                    case StaleMode.False:
                        _viewParams.Add("stale", "false");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("stale: " + stale);
                }
            }
        }

        public void AddOnErrorParam(OnErrorMode? onError)
        {
            if (onError != null)
            {
                switch (onError.Value)
                {
                    case OnErrorMode.Continue:
                        _viewParams.Add("on_error", "continue");
                        break;
                    case OnErrorMode.Stop:
                        _viewParams.Add("on_error", "stop");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("on_error: " + onError);
                }
            }
        }

        public Dictionary<string, string> Build()
        {
            return _viewParams.Count == 0 ? null : _viewParams;
        }



    }
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
 *    
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *    
 *        http://www.apache.org/licenses/LICENSE-2.0
 *    
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *    
 * ************************************************************/
#endregion