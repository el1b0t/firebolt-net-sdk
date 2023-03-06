﻿#region License Apache 2.0
/* Copyright 2022 
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

namespace FireboltDotNetSdk.Client
{
    public class FireResponse
    {
        public class LoginResponse
        {
            public LoginResponse(string access_token, string expires_in, string refresh_token, string token_type)
            {
                Access_token = access_token;
                Expires_in = expires_in;
                Refresh_token = refresh_token;
                Token_type = token_type;
            }
            /// <summary>
            /// Access token.
            /// </summary>
            public string Access_token { get; set; }

            /// <summary>
            /// Number of seconds after which token will expire.
            /// </summary>
            public string Expires_in { get; set; }

            /// <summary>
            /// Refresh token.
            /// </summary>
            public string Refresh_token { get; set; }

            /// <summary>
            /// Type of the token.
            /// </summary>
            public string Token_type { get; set; }

        }

        public class GetEngineUrlByDatabaseNameResponse
        {
            /// <summary>
            /// Retrieved record.
            /// </summary>
            public string? Engine_url { get; init; }

        }

        public class GetEngineUrlByEngineNameResponse
        {
            /// <summary>
            /// Retrieved record.
            /// </summary>
            public EngineDetails? engine { get; set; }

        }

        public class EngineDetails
        {
            /// <summary>
            /// Retrieved record.
            /// </summary>
            public string? endpoint { get; set; }
        }

        public class GetEngineNameByEngineIdResponse
        {
            /// <summary>
            /// Retrieved record.
            /// </summary>
            public Engine? engine_id { get; set; }

        }

        public class Engine
        {
            /// <summary>
            /// Retrieved record.
            /// </summary>
            public string? engine_id { get; set; }

            /// <summary>
            /// Retrieved record.
            /// </summary>
            public string? account_id { get; set; }

        }




        public class GetAccountIdByNameResponse
        {
            /// <summary>
            /// Retrieved record.
            /// </summary>
            public string? Account_id { get; init; }

        }
    }
}
