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

using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Mime;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FireboltDotNetSdk.Exception;
using FireboltDotNetSdk.Utils;

namespace FireboltDotNetSdk.Client
{
    /// <summary>
    /// Represents an SQL statement to execute against a FireBolt database. This class cannot be inherited.
    /// </summary>
    public class FireboltCommand : DbCommand
    {
        private FireboltConnection? _connection;
        private string? _commandText;
        private DbParameterCollection _parameters;

        public readonly HashSet<string> SetParamList;

        public FireboltCommand() : this(null, null)
        {
        }

        public FireboltCommand(FireboltConnection? connection, string? commandText, params DbParameter[] parameters) : this(connection, commandText, new FireboltParameterCollection(parameters))
        {
        }

        public FireboltCommand(FireboltConnection? connection, string? commandText, DbParameterCollection parameters)
        {
            _connection = connection;
            _commandText = commandText;
            _parameters = parameters;
            SetParamList = _connection?.SetParamList ?? new();
        }

        /// <summary>
        ///Gets or sets the SQL statement to execute at the data source.
        /// </summary>
        [AllowNull]
        public override string CommandText
        {
            get => _commandText ?? string.Empty;
            set => _commandText = value;
        }

        /// <summary>
        /// Gets the sets type of the command. The only supported type is <see cref="MediaTypeNames.Text"/>.
        /// </summary>
        /// <returns>The value <see cref="MediaTypeNames.Text"/>.</returns>
        /// <exception cref="NotSupportedException">The type set is not <see cref="MediaTypeNames.Text"/>.</exception>
        public override CommandType CommandType
        {
            get => CommandType.Text;

            set
            {
                if (value != CommandType.Text)
                    throw new NotSupportedException($"The type of the command \"{value}\" is not supported.");
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="FireboltConnection"/> used by this command.
        /// </summary>
        private new FireboltConnection? Connection
        {
            get => _connection;
            set => _connection = value;
        }

        /// <summary>
        /// Gets or sets the connection within which the command executes. Always returns <b>null</b>.
        /// </summary>
        /// <returns><b>null</b></returns>
        /// <exception cref="NotSupportedException">The value set is not <b>null</b>.</exception>
        protected override DbConnection? DbConnection
        {
            get => _connection;
            set => _connection = value == null ? null : (FireboltConnection)value;
        }


        /// <summary>
        /// Gets or sets the transaction within which the command executes. Always returns <b>null</b>.
        /// </summary>
        /// <returns><b>null</b></returns>
        /// <exception cref="NotSupportedException">The value set is not <b>null</b>.</exception>
        protected override DbTransaction? DbTransaction
        {
            get => null;
            set
            {
                if (value != null)
                {
                    throw new NotSupportedException($"{nameof(DbTransaction)} is read only.'");
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="FireboltParameterCollection"/>.
        /// </summary>
        /// <returns>The parameters of the SQL statement. The default is an empty collection.</returns>
        public new FireboltParameterCollection Parameters { get; } = new();

        /// <inheritdoc cref="Parameters"/>    
        protected sealed override DbParameterCollection DbParameterCollection => Parameters;

        public override int CommandTimeout
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        internal QueryResult? Execute(string commandText)
        {
            string? response = ExecuteCommandAsync(commandText).GetAwaiter().GetResult();
            return response == null ? new QueryResult() : GetOriginalJsonData(response);
        }

        private async Task<string?> ExecuteCommandAsync(string commandText)
        {
            if (Connection == null)
            {
                throw new FireboltException("Unable to execute SQL as no connection was initialised. Create command using working connection");
            }
            if (Connection.Client == null)
            {
                throw new FireboltException("Client is undefined. Initialize connection properly");
            }
            var engineUrl = Connection?.EngineUrl;
            if (commandText.Trim().StartsWith("SET"))
            {
                commandText = commandText.Remove(0, 4).Trim();
                SetParamList.Add(commandText);
                return await Task.FromResult<string?>(null);
            }
            string newCommandText = commandText;
            if (Parameters.Any())
            {
                newCommandText = GetParamQuery(commandText);
            }

            var database = Connection?.Database != string.Empty ? Connection?.Database : null;
            Task<string?> t = Connection!.Client.ExecuteQuery(engineUrl, database, Connection?.AccountId, SetParamList, newCommandText);
            return await t;
        }


        /// <summary>
        /// Get query with ready parse parameters<b>null</b>.
        /// </summary>
        /// <returns><b>null</b></returns>
        private string GetParamQuery(string commandText)
        {
            var escape_chars = new Dictionary<string, string>
            {
                { "\0", "\\0" },
                { "\\", "\\\\" },
                { "'", "\\'" }
            };
            try
            {
                foreach (var item in Parameters.ToList())
                {
                    string pattern = string.Format(@"\{0}\b", item.ParameterName);
                    RegexOptions regexOptions = RegexOptions.IgnoreCase;
                    var verifyParameters = item.Value?.ToString() ?? "";
                    if (item.Value is string && item.Value != null)
                    {
                        string? sourceText = item.Value.ToString();
                        if (sourceText == null)
                            throw new FireboltException("Unexpected error: Unable to cast string value to string.");
                        foreach (var item1 in escape_chars)
                        {
                            sourceText = sourceText.Replace(item1.Key, item1.Value);
                        }

                        verifyParameters = "'" + sourceText + "'";
                    }
                    else if (item.Value is DateTime)
                    {
                        DateTime dt = (DateTime)item.Value;
                        string date_str = dt.ToString("yyyy-MM-dd HH:mm:ss");
                        date_str = dt.Hour == 0 && dt.Minute == 0 && dt.Second == 0 ? date_str.Split(' ')[0] : date_str;
                        verifyParameters = new string("'" + date_str + "'");
                    }
                    else if (item.Value is null || item.Value.ToString() == string.Empty)
                    {
                        verifyParameters = "NULL";
                    }
                    else if (item.Value is bool)
                    {
                        verifyParameters = (bool)item.Value ? "1" : "0";
                    }
                    else if (item.Value is IList && item.Value.GetType().IsGenericType)
                    {
                        throw new FireboltException("Array query parameters are not supported yet.");
                    }
                    else if (item.Value is IConvertible)
                    {
                        // IConvertable is s a common interface for many numeric types.
                        // String representation of numbers (result of ToString()) depends on the current locale. 
                        // Some locales use comma instead or period to separate integer from the fractional part of number, 
                        // so making this representation portable requires replacing comma by dot in string. 
                        // The easier solution is to specify "standard" locale e.g. en_US.
                        verifyParameters = ((IConvertible)item.Value).ToString(new CultureInfo("en-US", false));
                    }
                    commandText = Regex.Replace(commandText, pattern, verifyParameters, regexOptions);
                }
                return commandText;
            }
            catch (System.Exception ex)
            {
                throw new FireboltException("Error while verifying parameters for query", ex);
            }
        }

        /// <summary>
        /// Gets original data in JSON format for further manipulation<b>null</b>.
        /// </summary>
        /// <returns><b>null</b></returns>
        private QueryResult? GetOriginalJsonData(string? Response)
        {
            if (Response == null) throw new FireboltException("Response is empty while GetOriginalJSONData");
            var prettyJson = JToken.Parse(Response).ToString(Formatting.Indented);
            return JsonConvert.DeserializeObject<QueryResult>(prettyJson);
        }

        public void ClearSetList()
        {
            _connection?.SetParamList.Clear();
        }

        /// <summary>
        /// Not supported. To cancel a command execute it asynchronously with an appropriate cancellation token.
        /// </summary>
        /// <exception cref="NotImplementedException">Always throws <see cref="NotImplementedException"/>.</exception>
        public override void Cancel()
        {
            throw new NotImplementedException();
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public override bool DesignTimeVisible
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        new public DbDataReader ExecuteReader()
        {
            return ExecuteReader(CommandBehavior.Default);
        }
        new public DbDataReader ExecuteReader(CommandBehavior behavior)
        {
            return ExecuteDbDataReader(behavior);
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if (_commandText == null)
            {
                throw new InvalidOperationException("Command is undefined");
            }
            QueryResult? result = Execute(_commandText);
            if (result == null)
            {
                throw new InvalidOperationException("No result produced");
            }
            return new FireboltDataReader(null, result, 0);
        }

        protected override DbParameter CreateDbParameter()
        {
            return new FireboltParameter();
        }

        public override int ExecuteNonQuery()
        {
            return ExecuteNonQueryAsync().GetAwaiter().GetResult();
        }

        public override object ExecuteScalar()
        {
            throw new NotImplementedException();
        }

        public override void Prepare()
        {
            throw new NotImplementedException();
        }

        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            if (_commandText == null)
            {
                throw new InvalidOperationException("SQL command is null");
            }
            await ExecuteCommandAsync(_commandText);
            return await Task.FromResult<int>(0);
        }
    }
}
