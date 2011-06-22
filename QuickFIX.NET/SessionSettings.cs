﻿using System.IO;
using System.Collections.Generic;

namespace QuickFix
{
    public class SessionSettings
    {
        #region Public Constants
        
        public const string BEGINSTRING  = "BeginString";
        public const string SENDERCOMPID = "SenderCompID";
        public const string TARGETCOMPID = "TargetCompID";
        public const string SESSION_QUALIFIER = "SessionQualifier";
        public const string DEFAULT_APPLVERID = "DefaultApplVerID";
        public const string CONNECTION_TYPE = "ConnectionType";

        #endregion

        #region Private Members
        
        private QuickFix.Dictionary defaults_ = new QuickFix.Dictionary();
        private System.Collections.Generic.Dictionary<SessionID, QuickFix.Dictionary> settings_ = new Dictionary<SessionID, Dictionary>();
        
        #endregion

        #region Constructors

        public SessionSettings(string file)
        {
            try
            {
                FileStream fs = File.Open(file, FileMode.Open, FileAccess.Read);
                Load(new StreamReader(fs));
            }
            catch(System.Exception e)
            {
                throw new ConfigError("File " + file + " not found (" + e.Message + ")");
            }
        }

        public SessionSettings(TextReader conf)
        {
            Load(conf);
        }

        public SessionSettings()
        { }

        #endregion

        public static bool IsComment(string s)
        {
            if (s.Length < 1)
                return false;
            return '#' == s[0];
        }

        public static bool IsKeyValue(string s)
        {
            return s.IndexOf('=') != -1;
        }

        public static bool IsSection(string s)
        {
            if (s.Length < 2)
                return false;
            return s[0] == '[' && s[s.Length - 1] == ']';
        }

        /// <summary>
        /// Strip the outer '[' and ']' from the section name, e.g. '[DEFAULT]' becomes 'DEFAULT'
        /// </summary>
        /// <param name="s">the section name</param>
        /// <returns></returns>
        public static string SplitSection(string s)
        {
            return s.Trim('[', ']').Trim();
        }

        protected void Load(TextReader conf)
        {
            Settings settings = new Settings();
            QuickFix.Dictionary currentSection = null;

            string line = null;
            while ((line = conf.ReadLine()) != null)
            {
                line = line.Trim();
                if (IsComment(line))
                {
                    continue;
                }
                else if (IsSection(line))
                {
                    currentSection = settings.Add(new Dictionary(SplitSection(line)));
                }
                else if (IsKeyValue(line))
                {
                    string[] kv = line.Split('=');
                    if (currentSection != null)
                        currentSection.SetString(kv[0].Trim(), kv[1].Trim());
                }
            }

            //---- load the DEFAULT section
            LinkedList<QuickFix.Dictionary> section = settings.Get("DEFAULT");
            QuickFix.Dictionary def = new QuickFix.Dictionary();
            if (section.Count > 0)
                def = section.First.Value;
            Set(def);

            //---- load each SESSION section
            section = settings.Get("SESSION");
            foreach (QuickFix.Dictionary dict in section)
            {
                dict.Merge(def);

                string sessionQualifier = "";
                if (dict.Has(SESSION_QUALIFIER))
                    sessionQualifier = dict.GetString(SESSION_QUALIFIER);
                SessionID sessionID = new SessionID(dict.GetString(BEGINSTRING), dict.GetString(SENDERCOMPID), dict.GetString(TARGETCOMPID), sessionQualifier);
                Set(sessionID, dict);
            }
        }

        public bool Has(SessionID sessionID)
        {
            return settings_.ContainsKey(sessionID);
        }

        /// <summary>
        /// Get global default settings
        /// </summary>
        /// <returns>Dictionary of settings from the [DEFAULT] section</returns>
        public QuickFix.Dictionary Get()
        {
            return defaults_;
        }

        /// <summary>
        /// Get a dictionary for a session
        /// </summary>
        /// <param name="sessionID">the ID of the session</param>
        /// <returns>Dictionary of settings from the [SESSION] section for the given SessionID</returns>
        public Dictionary Get(SessionID sessionID)
        {
            Dictionary dict;
            if (!settings_.TryGetValue(sessionID, out dict))
                throw new ConfigError("Session '" + sessionID + "' not found");
            return dict;
        }

        public void Set(QuickFix.Dictionary defaults)
        {
            defaults_ = defaults;
            foreach (KeyValuePair<SessionID, QuickFix.Dictionary> entry in settings_)
                entry.Value.Merge(defaults_);
        }
        
        public void Set(SessionID sessionID, QuickFix.Dictionary settings)
        {
            if (Has(sessionID))
                throw new ConfigError("Duplicate Session " + sessionID.ToString());
            settings.SetString(BEGINSTRING, sessionID.BeginString);
            settings.SetString(SENDERCOMPID, sessionID.SenderCompID);
            settings.SetString(TARGETCOMPID, sessionID.TargetCompID);
            settings.Merge(defaults_);
            Validate(settings);
            settings_[sessionID] = settings;
        }

        public HashSet<SessionID> GetSessions()
        {
            HashSet<SessionID> result = new HashSet<SessionID>();
            foreach (KeyValuePair<SessionID, QuickFix.Dictionary> entry in settings_)
                result.Add(entry.Key);
            return result;
        }

        public override string ToString()
        {
            System.Text.StringBuilder s = new System.Text.StringBuilder();
            s.AppendLine("[DEFAULT]");

            foreach (System.Collections.Generic.KeyValuePair<string, string> entry in defaults_)
                s.Append(entry.Key).Append('=').AppendLine(entry.Value);

            foreach (KeyValuePair<SessionID, QuickFix.Dictionary> entry in settings_)
            {
                s.AppendLine().AppendLine("[SESSION]");
                foreach (System.Collections.Generic.KeyValuePair<string, string> kvp in entry.Value)
                {
                    if (defaults_.Has(kvp.Key) && defaults_.GetString(kvp.Key).Equals(kvp.Value))
                        continue;
                    s.Append(kvp.Key).Append('=').AppendLine(kvp.Value);
                }
            }

            return s.ToString();
        }

        protected void Validate(QuickFix.Dictionary dictionary)
        {
            string beginString = dictionary.GetString(BEGINSTRING);
            if (beginString != Values.BeginString_FIX40 &&
                beginString != Values.BeginString_FIX41 &&
                beginString != Values.BeginString_FIX42 &&
                beginString != Values.BeginString_FIX43 &&
                beginString != Values.BeginString_FIX44 &&
                beginString != Values.BeginString_FIXT11)
            {
                throw new ConfigError(BEGINSTRING + " (" + beginString + ") must be FIX.4.0 to FIX.4.4 or FIXT.1.1");
            }

            string connectionType = dictionary.GetString(CONNECTION_TYPE);
            if (connectionType != "initiator" && connectionType != "acceptor")
            {
                throw new ConfigError(CONNECTION_TYPE + " must be 'initiator' or 'acceptor'");
            }
        }
    }
}