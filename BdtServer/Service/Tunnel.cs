// -----------------------------------------------------------------------------
// BoutDuTunnel
// Sebastien LEBRETON
// sebastien.lebreton[-at-]free.fr
// -----------------------------------------------------------------------------

#region " Inclusions "
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

using Bdt.Server.Resources;
using Bdt.Shared.Configuration;
using Bdt.Shared.Logs;
using Bdt.Shared.Request;
using Bdt.Shared.Response;
using Bdt.Shared.Service;
using System.Net;
using Bdt.Shared.Runtime;
#endregion

namespace Bdt.Server.Service
{

    /// -----------------------------------------------------------------------------
    /// <summary>
    /// Le tunnel, assure les services de l'interface ITunnel
    /// </summary>
    /// -----------------------------------------------------------------------------
    public class Tunnel : MarshalByRefObject, ITunnel, ILogger
    {

        #region " Constantes "
        public const int BUFFER_SIZE = 32768;
        public const int POLLING_TIME = 1000 * 60; // msec -> 1m

        protected const string CONFIG_USER_TEMPLATE = "users/{0}@";
        protected const string CONFIG_USER_ENABLED = "enabled";
        protected const string CONFIG_USER_PASSWORD = "password";
        protected const string CONFIG_USER_ADMIN = "admin";
        protected const string CONFIG_SESSION_TIMEOUT = "stimeout";
        protected const string CONFIG_CONNECTION_TIMEOUT = "ctimeout";

        protected const int DEFAULT_CONNECTION_TIMEOUT_DELAY = 1; // heures
        protected const int DEFAULT_SESSION_TIMEOUT_DELAY = 12; // heures
        #endregion

        #region " Attributs "
        protected Dictionary<int, TunnelSession> m_sessions = new Dictionary<int, TunnelSession>();
        protected static ManualResetEvent m_mre = new ManualResetEvent(false);
        protected static ConfigPackage m_configuration;
        protected static ILogger m_logger = null;
        #endregion

        #region " Proprietes "
        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Les sessions
        /// </summary>
        /// -----------------------------------------------------------------------------
        protected virtual Dictionary<int, TunnelSession> Sessions
        {
            get
            {
                return m_sessions;
            }
            set
            {
                m_sessions = value;
            }
        }
        /// -----------------------------------------------------------------------------
        /// <summary>
        /// La configuration serveur (d'après le fichier xml + ligne de commande)
        /// </summary>
        /// -----------------------------------------------------------------------------
        public static ConfigPackage Configuration
        {
            get
            {
                return m_configuration;
            }
            set
            {
                m_configuration = value;
            }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Le loggeur
        /// </summary>
        /// -----------------------------------------------------------------------------
        public static ILogger Logger
        {
            get
            {
                return m_logger;
            }
            set
            {
                m_logger = value;
            }
        }
        #endregion

        #region " Méthodes "
        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Constructeur
        /// </summary>
        /// -----------------------------------------------------------------------------
        public Tunnel()
        {
            Thread thr = new Thread(new System.Threading.ThreadStart(CheckerThread));
            thr.Start();
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Generation d'id non encore présent dans une table de hash
        /// </summary>
        /// <param name="hash">la table à analyser</param>
        /// <returns>un entier unique</returns>
        /// -----------------------------------------------------------------------------
        internal static int GetNewId(System.Collections.IDictionary hash)
        {
            Random rnd = new Random();
            int key = rnd.Next(0, int.MaxValue);
            while (hash.Contains(key))
            {
                key += 1;
            }
            return key;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Generation d'un identifiant session unique
        /// </summary>
        /// <returns>un entier unique</returns>
        /// -----------------------------------------------------------------------------
        protected int GetNewSid()
        {
            return GetNewId(Sessions);
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Destructeur
        /// </summary>
        /// -----------------------------------------------------------------------------
        ~Tunnel()
        {
            DisableChecking();
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Annule la vérification
        /// </summary>
        /// -----------------------------------------------------------------------------
        public static void DisableChecking()
        {
            m_mre.Set();
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Fixe une durée de vie au tunnel
        /// </summary>
        /// -----------------------------------------------------------------------------
        public override object InitializeLifetimeService()
        {
            return null;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Traitement principal de thread de vérification des connexions et sessions
        /// </summary>
        /// -----------------------------------------------------------------------------
        protected void CheckerThread()
        {
            while (!m_mre.WaitOne(POLLING_TIME, false))
            {
                TimeoutObject.CheckTimeout(this, Sessions);
            }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Vérification de l'authentification d'un utilisateur associé à une requête
        /// </summary>
        /// <param name="request">la requête</param>
        /// <param name="response">la réponse à préparer</param>
        /// <returns>La session si l'utilisateur est authentifié</returns>
        /// -----------------------------------------------------------------------------
        public TunnelSession CheckSession<I, O> (ref I request, ref O response)
            where I : ISessionContextRequest
            where O : IMinimalResponse
        {
            TunnelSession session;
            if (!Sessions.TryGetValue(request.Sid, out session))
            {
                response.Success = false;
                response.Message = Strings.SERVER_SIDE + Strings.SID_NOT_FOUND;
                return null;
            }
            else
            {
                session.LastAccess = DateTime.Now;
                response.Success = true;
                response.Message = string.Empty;
                return session;
            }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Login d'un utilisateur
        /// </summary>
        /// <param name="request">la requête</param>
        /// <returns>une réponse LoginResponse avec un sid</returns>
        /// -----------------------------------------------------------------------------
        public LoginResponse Login(LoginRequest request)
        {
            bool enabled = Configuration.ValueBool(string.Format(CONFIG_USER_TEMPLATE, request.Username) + CONFIG_USER_ENABLED, false);
            string password = Configuration.Value(string.Format(CONFIG_USER_TEMPLATE, request.Username) + CONFIG_USER_PASSWORD, String.Empty);
            bool admin = Configuration.ValueBool(string.Format(CONFIG_USER_TEMPLATE, request.Username) + CONFIG_USER_ADMIN, false);
            int stimeout = Configuration.ValueInt(string.Format(CONFIG_USER_TEMPLATE, request.Username) + CONFIG_SESSION_TIMEOUT, int.MinValue);
            int ctimeout = Configuration.ValueInt(string.Format(CONFIG_USER_TEMPLATE, request.Username) + CONFIG_CONNECTION_TIMEOUT, int.MinValue);

            string message = String.Empty;
            bool success = false;
            int sid = -1;

            if (!enabled)
            {
                message = Strings.SERVER_SIDE + String.Format(Strings.ACCESS_DENIED,request.Username);
            }
            else
            {
                if (password == request.Password)
                {
                    // Vérifications des timeouts
                    if (stimeout == int.MinValue)
                    {
                        stimeout = DEFAULT_SESSION_TIMEOUT_DELAY;
                        Log(String.Format(Strings.DEFAULT_SESSION_TIMEOUT, request.Username, stimeout), ESeverity.WARN);
                    }
                    if (ctimeout == int.MinValue)
                    {
                        ctimeout = DEFAULT_CONNECTION_TIMEOUT_DELAY;
                        Log(String.Format(Strings.DEFAULT_CONNECTION_TIMEOUT, request.Username, ctimeout), ESeverity.WARN);
                    }

                    TunnelSession session = new TunnelSession(stimeout, ctimeout);
                    session.Logon = DateTime.Now;
                    sid = GetNewSid();
                    session.Username = request.Username;
                    session.LastAccess = DateTime.Now;
                    session.Admin = admin;
                    Sessions.Add(sid, session);
                    message = Strings.SERVER_SIDE + string.Format(Strings.ACCESS_GRANTED, request.Username);
                    success = true;
                }
                else
                {
                    message = Strings.SERVER_SIDE + string.Format(Strings.ACCESS_DENIED_BAD_PASSWORD, request.Username);
                }
            }

            Log(message, ESeverity.INFO);
            return new LoginResponse(success, message, sid);
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Logout d'un utilisateur
        /// </summary>
        /// <param name="request">la requête</param>
        /// <returns>une réponse générique</returns>
        /// -----------------------------------------------------------------------------
        public MinimalResponse Logout(SessionContextRequest request)
        {
            MinimalResponse response = new MinimalResponse();
            TunnelSession session = CheckSession(ref request, ref response);

            if (session != null)
            {
                try
                {
                    session.DisconnectAndRemoveAllConnections();
                    Sessions.Remove(request.Sid);

                    response.Success = true;
                    response.Message = Strings.SERVER_SIDE + String.Format(Strings.SESSION_LOGOUT, session.Username);
                }
                catch (Exception ex)
                {
                    response.Success = false;
                    response.Message = Strings.SERVER_SIDE + ex.Message;
                }
            }

            Log(response.Message, ESeverity.INFO);
            return response;
        }
        
        /// -----------------------------------------------------------------------------
        /// <summary>
        /// La version du serveur
        /// </summary>
        /// <returns>La version de l'assembly</returns>
        /// -----------------------------------------------------------------------------
        public MinimalResponse Version()
        {
            System.Reflection.AssemblyName name = this.GetType().Assembly.GetName();
            return new MinimalResponse(true, Strings.SERVER_SIDE + string.Format("{0} v{1}, {2}", name.Name, name.Version.ToString(3), Bdt.Shared.Runtime.Program.FrameworkVersion()));
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Etablissement d'une nouvelle connexion
        /// </summary>
        /// <param name="request">la requête</param>
        /// <returns>le cid de connexion</returns>
        /// -----------------------------------------------------------------------------
        public ConnectResponse Connect(ConnectRequest request)
        {
            ConnectResponse response = new ConnectResponse();
            TunnelSession session = CheckSession(ref request, ref response);

            if ( session != null )
            {
                TunnelConnection connection = session.CreateConnection();
                try
                {
                    connection.TcpClient = new TcpClient(request.Address, request.Port);
                    connection.Stream = connection.TcpClient.GetStream();
                    connection.LastAccess = DateTime.Now;
                    connection.ReadCount = 0;
                    connection.WriteCount = 0;

                    try
                    {
                        connection.Host = Dns.GetHostEntry(((IPEndPoint)connection.TcpClient.Client.RemoteEndPoint).Address).HostName;
                    }
                    catch (Exception)
                    {
                        connection.Host = "?";
                    }
                    
                    response.Connected = true;
                    response.DataAvailable = connection.Stream.DataAvailable;

                    int cid = session.AddConnection(connection);
                    response.Success = true;
                    response.Message = Strings.SERVER_SIDE + string.Format(Strings.CONNECTED, connection.TcpClient.Client.RemoteEndPoint, request.Address);
                    response.Cid = cid;
                }
                catch (Exception ex)
                {
                    response.Success = false;
                    response.Message = Strings.SERVER_SIDE + string.Format(Strings.CONNECTION_REFUSED, request.Address, request.Port, ex.Message);
                    response.Cid = -1;
                }
            }

            Log(response.Message, ESeverity.INFO);
            return response;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Déconnexion
        /// </summary>
        /// <param name="request">la requête</param>
        /// <returns>une reponse générique</returns>
        /// -----------------------------------------------------------------------------
        public ConnectionContextResponse Disconnect(ConnectionContextRequest request)
        {
            ConnectionContextResponse response = new ConnectionContextResponse();
            TunnelSession session = CheckSession(ref request, ref response);

            if ( session != null )
            {
                TunnelConnection connection = session.CheckConnection(ref request, ref response);
                if (connection != null)
                {
                    try
                    {
                        response.Message = Strings.SERVER_SIDE + string.Format(Strings.DISCONNECTED, connection.TcpClient.Client.RemoteEndPoint.ToString());

                        connection.SafeDisconnect();
                        response.Connected = false;
                        response.DataAvailable = false;
                        response.Success = true;
                        session.RemoveConnection(request.Cid);
                    }
                    catch (Exception ex)
                    {
                        response.Success = false;
                        response.Message = Strings.SERVER_SIDE + ex.Message;
                    }
                }
            }

            Log(response.Message, ESeverity.INFO);
            return response;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Lecture de données
        /// </summary>
        /// <param name="request">la requête</param>
        /// <returns>les données lues dans response.Data</returns>
        /// -----------------------------------------------------------------------------
        public ReadResponse Read(ConnectionContextRequest request)
        {
            ReadResponse response = new ReadResponse();
            TunnelSession session = CheckSession(ref request, ref response);

            if ( session != null )
            {
                TunnelConnection connection = session.CheckConnection(ref request, ref response);
                if (connection != null)
                {
                    if (response.Connected && response.DataAvailable)
                    {
                        // Données disponibles
                        try
                        {
                            byte[] buffer = new byte[BUFFER_SIZE];
                            int count = connection.Stream.Read(buffer, 0, BUFFER_SIZE);
                            if (count > 0)
                            {
                                Array.Resize(ref buffer, count);
                                response.Success = true;
                                response.Message = string.Empty;
                                Program.StaticXorEncoder(ref buffer, request.Cid);
                                response.Data = buffer;
                                connection.ReadCount += count;
                            }
                            else
                            {
                                response.Success = false;
                                response.Data = null;
                                response.Message = Strings.SERVER_SIDE + Strings.DISCONNECTION_DETECTED;
                            }
                        }
                        catch (Exception ex)
                        {
                            response.Success = false;
                            response.Data = null;
                            response.Message = Strings.SERVER_SIDE + ex.Message;
                        }
                    }
                    else
                    {
                        // Pas de données disponibles
                        response.Success = true;
                        response.Message = string.Empty;
                        response.Data = new byte[] { };
                    }
                }
            }

            return response;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Ecriture de données
        /// </summary>
        /// <param name="request">la requête</param>
        /// <returns>une réponse générique</returns>
        /// -----------------------------------------------------------------------------
        public ConnectionContextResponse Write(WriteRequest request)
        {
            ConnectionContextResponse response = new ConnectionContextResponse();
            TunnelSession session = CheckSession(ref request, ref response);

            if ( session != null )
            {
                TunnelConnection connection = session.CheckConnection(ref request, ref response);
                if (connection != null)
                {
                    try
                    {
                        byte[] result = request.Data;
                        Program.StaticXorEncoder(ref result, request.Cid);
                        connection.Stream.Write(result, 0, result.Length);
                        response.Success = true;
                        response.Message = string.Empty;
                        connection.WriteCount += result.Length;
                    }
                    catch (Exception ex)
                    {
                        response.Success = false;
                        response.Message = Strings.SERVER_SIDE + ex.Message;
                    }
                }
            }

            return response;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Enumeration des session
        /// </summary>
        /// <param name="request">la requête</param>
        /// <returns>les sessions dans response.Sessions</returns>
        /// -----------------------------------------------------------------------------
        public MonitorResponse Monitor(SessionContextRequest request)
        {

            MonitorResponse response = new MonitorResponse();
            TunnelSession session = CheckSession(ref request, ref response);

            if (session != null)
            {
                try
                {
                    if (session.Admin)
                    {
                        List<Session> exportsessions = new List<Session>();

                        foreach (int sid in Sessions.Keys)
                        {
                            TunnelSession cursession = Sessions[sid];
                            Session export = new Session();
                            export.Sid = sid.ToString("x");
                            export.Admin = cursession.Admin;
                            export.Connections = cursession.GetConnectionsStruct();
                            export.LastAccess = cursession.LastAccess;
                            export.Logon = cursession.Logon;
                            export.Username = cursession.Username;
                            exportsessions.Add(export);
                        }

                        response.Sessions = exportsessions.ToArray();
                        response.Success = true;
                        response.Message = string.Empty;
                    }
                    else
                    {
                        response.Success = false;
                        response.Sessions = null;
                        response.Message = Strings.SERVER_SIDE + Strings.ADMIN_REQUIRED;
                    }
                }
                catch (Exception ex)
                {
                    response.Success = false;
                    response.Sessions = null;
                    response.Message = Strings.SERVER_SIDE + ex.Message;
                }
            }

            return response;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Demande de suppression d'une session
        /// </summary>
        /// <param name="request">la requête</param>
        /// <returns>une réponse générique</returns>
        /// -----------------------------------------------------------------------------
        public MinimalResponse KillSession(KillSessionRequest request)
        {
            MinimalResponse response = new MinimalResponse();
            TunnelSession targetsession = CheckSession(ref request, ref response);

            if (targetsession != null)
            {
                SessionContextRequest fake = new SessionContextRequest(request.AdminSid);
                TunnelSession adminsession = CheckSession(ref fake, ref response);
                if (adminsession != null)
                {
                    if (adminsession.Admin)
                    {
                        try
                        {
                            targetsession.DisconnectAndRemoveAllConnections();
                            Sessions.Remove(request.Sid);

                            response.Success = true;
                            response.Message = Strings.SERVER_SIDE + String.Format(Strings.SESSION_KILLED, targetsession.Username, adminsession.Username);

                        }
                        catch (Exception ex)
                        {
                            response.Success = false;
                            response.Message = Strings.SERVER_SIDE + ex.Message;
                        }
                    }
                    else
                    {
                        response.Success = false;
                        response.Message = Strings.SERVER_SIDE + Strings.ADMIN_REQUIRED;
                    }
                }
            }

            Log(response.Message, ESeverity.INFO);
            return response;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Demande de suppression d'une connexion
        /// </summary>
        /// <param name="request">la requête</param>
        /// <returns>une réponse générique</returns>
        /// -----------------------------------------------------------------------------
        public ConnectionContextResponse KillConnection(KillConnectionRequest request)
        {
            ConnectionContextResponse response = new ConnectionContextResponse();
            TunnelSession targetsession = CheckSession(ref request, ref response);

            if (targetsession != null)
            {
                TunnelConnection targetconnection = targetsession.CheckConnection(ref request, ref response);
                if (targetconnection != null)
                {
                    SessionContextRequest fake = new SessionContextRequest(request.AdminSid);
                    TunnelSession adminsession = CheckSession(ref fake, ref response);
                    if (adminsession != null)
                    {
                        if (adminsession.Admin)
                        {
                            try
                            {
                                response.Message = Strings.SERVER_SIDE + string.Format(Strings.CONNECTION_KILLED, targetconnection.TcpClient.Client.RemoteEndPoint, targetsession.Username, adminsession.Username);

                                targetconnection.SafeDisconnect();
                                response.Connected = false;
                                response.DataAvailable = false;
                                response.Success = true;
                                targetsession.RemoveConnection(request.Cid);
                            }
                            catch (Exception ex)
                            {
                                response.Success = false;
                                response.Message = Strings.SERVER_SIDE + ex.Message;
                            }
                        }
                        else
                        {
                            response.Success = false;
                            response.Message = Strings.SERVER_SIDE + Strings.ADMIN_REQUIRED;
                        }
                    }
                }
            }

            Log(response.Message, ESeverity.INFO);
            return response;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Ecriture d'une entrée de log. Ne sera pas prise en compte si le log est inactif
        /// ou si le filtre l'impose
        /// </summary>
        /// <param name="sender">l'emetteur</param>
        /// <param name="message">le message à logger</param>
        /// <param name="severity">la sévérité</param>
        /// -----------------------------------------------------------------------------
        public void Log(object sender, string message, ESeverity severity)
        {
            if (m_logger != null)
            {
                m_logger.Log(sender, message, severity);
            }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Ecriture d'une entrée de log. Ne sera pas prise en compte si le log est inactif
        /// ou si le filtre l'impose
        /// </summary>
        /// <param name="message">le message à logger</param>
        /// <param name="severity">la sévérité</param>
        /// -----------------------------------------------------------------------------
        public void Log(string message, ESeverity severity)
        {
            Log(this, message, severity);
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Fermeture du logger
        /// </summary>
        /// -----------------------------------------------------------------------------
        public void Close()
        {
            if (m_logger != null)
            {
                m_logger.Close();
            }
        }
        #endregion

    }

}


