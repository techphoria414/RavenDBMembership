﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Web.Configuration;
using System.Web.Security;
using NUnit.Framework;

namespace RavenDBMembership.IntegrationTests.ProviderFixtures
{
    public abstract class MembershipProviderOverride
    {
        public abstract MembershipProvider GetProvider();
        public virtual bool IsSqlMembershipProvider()
        {
            return false; 
        }
        public virtual void PostInitializeUpdate(MembershipProvider provider) { }

        MembershipProvider _originalProvider;
        MembershipProvider _injectedProvider;

        public void InjectMembershipImplementation(IEnumerable<KeyValuePair<string, string>> simulatedAppConfigSettings)
        {
            if (MembershipIsInitialized)
                _originalProvider = MembershipProvider;
            else
                _originalProvider = null;

            _injectedProvider = GetProvider();

            InitializeMembershipProviderFromConfigEntry(_injectedProvider, simulatedAppConfigSettings);

            PostInitializeUpdate(_injectedProvider);

            MembershipProvider = _injectedProvider;
            MembershipIsInitialized = true;

            MembershipProviders = new MembershipProviderCollection();
            MembershipProviders.Add(_injectedProvider);
            MembershipProviders.SetReadOnly();
        }

        public void RestoreMembershipImplementation()
        {
            if (MembershipProvider == _injectedProvider)
            {
                var currentProviderDisposable = MembershipProvider as IDisposable;

                if (currentProviderDisposable != null)
                    currentProviderDisposable.Dispose();

                MembershipIsInitialized = _originalProvider != null;
                MembershipProvider = _originalProvider;
                _originalProvider = null;
            }
        }

        static bool MembershipIsInitialized
        {
            get { return (bool)MembershipInitializedField.GetValue(Membership.Provider); }
            set { MembershipInitializedField.SetValue(Membership.Provider, value); }
        }

        static MembershipProvider MembershipProvider
        {
            get { return MembershipProviderField.GetValue(null) as MembershipProvider; }
            set { MembershipProviderField.SetValue(null, value); }
        }

        static MembershipProviderCollection MembershipProviders
        {
            get { return MembershipProvidersField.GetValue(null) as MembershipProviderCollection; }
            set { MembershipProvidersField.SetValue(null, value); }
        }

        static FieldInfo MembershipInitializedField = typeof(Membership).GetField("s_Initialized",
                                                                                   BindingFlags.Static |
                                                                                   BindingFlags.NonPublic);

        static FieldInfo MembershipProviderField = typeof(Membership).GetField("s_Provider",
                                                                                BindingFlags.Static |
                                                                                BindingFlags.NonPublic);

        static FieldInfo MembershipProvidersField = typeof (Membership).GetField("s_Providers",
                                                                                 BindingFlags.Static |
                                                                                 BindingFlags.NonPublic);

        public void InitializeMembershipProviderFromConfigEntry(MembershipProvider result,
            IEnumerable<KeyValuePair<string, string>> simulatedAppConfigSettings)
        {
            NameValueCollection nameValueCollection = null;

            MembershipSection membership = ConfigurationManager.GetSection("system.web/membership") as MembershipSection;

            foreach (ProviderSettings settings in membership.Providers)
            {
                if (settings.Name == FixtureConstants.NameOfConfiguredMembershipProvider)
                {
                    nameValueCollection = new NameValueCollection(settings.Parameters);
                    break;
                }
            }

            if (nameValueCollection == null)
            {
                throw new Exception("Configuration not found for membership provider RavenDBMembership.");
            }

            nameValueCollection["connectionStringName"] = "StubConnectionString";

            foreach (var kvp in simulatedAppConfigSettings)
            {
                ValidateConfigurationValue(kvp.Key, kvp.Value);
                nameValueCollection.Set(kvp.Key, kvp.Value);
            }

            result.Initialize(FixtureConstants.NameOfConfiguredMembershipProvider, nameValueCollection);
        }

        public void ValidateConfigurationValue(string name, string value)
        {
            if (name.ToLower().Equals("name") || name.ToLower().Equals("type"))
                throw new Exception("Tried to set a configuration property that is required by MembershipProvider.Initialize.");


            string[] expectedConfigurationValues = new[]
            {
                "connectionStringName","enablePasswordRetrieval", "enablePasswordReset", "requiresQuestionAndAnswer", "requiresUniqueEmail",
                "maxInvalidPasswordAttempts", "minRequiredPasswordLength", "minRequiredNonalphanumericCharacters", "passwordAttemptWindow=",
                "applicationName"
            };

            if (!expectedConfigurationValues.Contains(name))
            {
                throw new ArgumentException(
                    "MembershipProviderOverride was asked to configure unknown MembershipProvider setting '" +
                    (name ?? "<null") + ".");
            }
        }

    }
}