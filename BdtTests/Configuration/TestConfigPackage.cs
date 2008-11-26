// -----------------------------------------------------------------------------
// BoutDuTunnel
// Sebastien LEBRETON
// sebastien.lebreton[-at-]free.fr
// -----------------------------------------------------------------------------

#region " Inclusions "
using System;
using System.Collections.Generic;
using Bdt.Tests.Model;
#endregion

namespace Bdt.Shared.Configuration
{

    /// -----------------------------------------------------------------------------
    /// <summary>
    /// Regroupe un ensemble de sources de configurations priorisées. Permet de rechercher des
    /// valeurs suivant un code.
    /// Les sources peuvent être différentes: base de donnée, fichier de configuration,
    /// ligne de commande, etc
    /// </summary>
    /// -----------------------------------------------------------------------------
    public sealed class TestConfigPackage : ConfigPackage
    {
        #region " Constantes "
        public const string USER_LOGIN = "usertest";
        public const string USER_PASSWORD = "userpassword";
        public const string USER_DISABLED_LOGIN = "userdisabled";
        public const string USER_DISABLED_PASSWORD = "userdisabledpassword";
        public const string USER_LAMBDA_LOGIN = "userlambda";
        public const string USER_LAMBDA_PASSWORD = "userlambdapassword";
        #endregion

        #region " Attributs "
        private string m_protocol = "";
        private string m_port = "";
        #endregion

        #region " Propriétés "
        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Retourne la valeur d'un élément suivant son code
        /// </summary>
        /// <param name="code">le code de l'élément</param>
        /// <param name="defaultValue">la valeur par défaut si l'élément est introuvable</param>
        /// <returns>La valeur de l'élément s'il existe ou defaultValue sinon</returns>
        /// -----------------------------------------------------------------------------
        public override string Value(string code, string defaultValue)
        {
            if (code.StartsWith("forward/")) {
                if (code.EndsWith("@enabled") || code.EndsWith("@shared"))
                {
                    return "false";
                }
                return "0";
            }

            switch (code)
            {
                case "service@username": return USER_LOGIN;
                case "service@password": return USER_PASSWORD;
                case "service@protocol": return m_protocol;
                case "service@name": return "BdtTestService";
                case "service@port": return m_port;
                case "service@address": return "localhost";
                case "service@culture": return string.Empty;

                case "users/usertest@password": return "userpassword";
                case "users/usertest@enabled": return "true";
                case "users/usertest@admin": return "true";

                case "users/userdisabled@password": return USER_DISABLED_PASSWORD;
                case "users/userdisabled@enabled": return "false";

                case "users/userlambda@password": return USER_LAMBDA_PASSWORD;
                case "users/userlambda@enabled": return "true";
                case "users/userlambda@admin": return "false";
                
                case "socks@enabled": return "false";
                case "socks@shared": return "false";
                case "socks@port": return "1080";

                case "proxy@enabled": return "false";
                case "proxy/configuration@auto": return "false";
                case "proxy/configuration@address": return string.Empty;
                case "proxy/configuration@port": return string.Empty;
                case "proxy/authentification@auto": return "false";
                case "proxy/authentification@password": return string.Empty;
                case "proxy/authentification@username": return string.Empty;
                case "proxy/authentification@domain": return string.Empty;

                default: return string.Empty; // throw new ArgumentException(code);
            }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Constructeur
        /// </summary>
        /// <param name="protocol">le protocole à utiliser</param>
        /// <param name="port">le port de base</param>
        /// -----------------------------------------------------------------------------
        public TestConfigPackage(String protocol, int port)
        {
            m_protocol = protocol;
            m_port = port.ToString();
        }
        #endregion

    }

}

