//--------------------------------------------------------------------------------
//<FileInfo>
//  <copyright file="BaseContext.cs" company="Critical Manufacturing, SA">
//        <![CDATA[Copyright © Critical Manufacturing SA. All rights reserved.]]>
//  </copyright>
//  <Author>João Brandão</Author>
//</FileInfo>
//--------------------------------------------------------------------------------

#region Using Directives

using Cmf.LightBusinessObjects.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Threading;

#endregion Using Directives

namespace Settings
{
    /// <summary>
    /// Represents the test base class
    /// </summary>
    public class BaseContext
    {
        #region Private Variables

        /// <summary>
        /// The configuration
        /// </summary>
        private static ClientConfiguration config = null;

        #endregion

        #region Public Variables
        #endregion

        #region Properties

        /// <summary>
        /// Gets the password.
        /// </summary>
        /// <value>
        /// The password.
        /// </value>
        public static string Password
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the name of the user.
        /// </summary>
        /// <value>
        /// The name of the user.
        /// </value>
        public static string UserName
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the user role.
        /// </summary>
        /// <value>
        /// The user role.
        /// </value>
        public static string UserRole
        {
            get;
            private set;
        }

        #endregion

        #region Constructors
        #endregion

        #region Public Methods

        /// <summary>
        /// Ends this instance.
        /// </summary>
        public static void BaseEnd()
        {
            // Assembly clean up
        }

        /// <summary>
        /// Initializes the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        public static void BaseInit(TestContext context)
        {
            BaseContext.UserName = GetString(context, "userName");
            BaseContext.Password = GetString(context, "password");

            ClientConfigurationProvider.ConfigurationFactory = () =>
            {
                if (config == null)
                {
                    config = new ClientConfiguration()
                    {
                        HostAddress = System.IO.Directory.Exists(GetString(context, "hostAdress")) ? GetString(context, "hostAdress") : string.Format("{0}:{1}", GetString(context, "hostAdress"), int.Parse(GetString(context, "hostPort"))),
                        ClientTenantName = GetString(context, "clientTenantName"),
                        UseSsl = context.Properties.Contains("hostUseSSL") ? bool.Parse(GetString(context, "hostUseSSL")) : false,
                        ApplicationName = GetString(context, "applicationName"),
                        IsUsingLoadBalancer = context.Properties.Contains("useLoadBalancer") ? bool.Parse(GetString(context, "useLoadBalancer")) : false,
                        ThingsToDoAfterInitialize = null,
                        RequestTimeout = GetString(context, "requestTimeout")
                    };

                    bool authenticateViaSecurityPortalToken = bool.TryParse(context.Properties["authenticateViaSecurityPortalToken"]?.ToString(), out bool authSecPortal) && authSecPortal;
                    if (authenticateViaSecurityPortalToken)
                    {
                        config.ClientId = context.Properties["securityPortalClientId"]?.ToString() ?? string.Empty;
                        config.SecurityPortalBaseAddress = new Uri(context.Properties["securityPortalBaseAddress"].ToString());
                        config.SecurityAccessToken = context.Properties["securityPortalAccessToken"]?.ToString() ?? string.Empty;
                    }
                    else
                    {
                        config.UserName = UserName;
                        config.Password = Password;
                    }
                }
                return config;
            };

            UserRole = context.Properties.Contains("userRole") ? GetString(context, "userRole") : "Almost Admin";

            // Handle Culture
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(GetString(context, "culture"));

            #region Set DB Connections
            //if (context.Properties.Contains("OnlineConnection"))
            //{
            //    Utilities.OnlineConnection = new SqlConnection(GetString(context, "OnlineConnection"));
            //}
            //if (context.Properties.Contains("ODSConnection"))
            //{
            //    Utilities.ODSConnection = new SqlConnection(GetString(context, "ODSConnection"));
            //}
            //if (context.Properties.Contains("DWHConnection"))
            //{
            //    Utilities.DWHConnection = new SqlConnection(GetString(context, "DWHConnection"));
            //}
            #endregion
        }

        /// <summary>
        /// Retries a given function after a period of time to avoid race condition
        /// </summary>
        public static void RetryRun(Action retryRun, int retryCount = 3, TimeSpan? span = null)
        {
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    retryRun();
                    break;
                }
                catch
                {
                    if (span == null)
                    {
                        Random random = new Random();
                        span = new TimeSpan(0, 0, random.Next(5, 15));
                    }

                    if (i + 1 == retryCount)
                    {
                        throw;
                    }
                    else
                    {
                        Thread.Sleep(span.Value);
                    }
                }
            }
        }
        #endregion

        #region Private & Internal Methods

        /// <summary>
        /// Gets a string from the TestContext properties
        /// </summary>
        /// <param name="context">Test Context</param>
        /// <param name="property">Property to find</param>
        /// <returns>A string</returns>
        private static string GetString(TestContext context, string property)
        {
            if (context.Properties[property] == null)
            {
                throw new ArgumentException($"Property does not exist, does not have a value, or a test setting is not selected.", property);
            }
            else
            {
                return context.Properties[property].ToString();
            }
        }

        #endregion

        #region Event handling Methods
        #endregion
    }
}
