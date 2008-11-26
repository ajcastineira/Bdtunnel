// -----------------------------------------------------------------------------
// BoutDuTunnel
// Sebastien LEBRETON
// sebastien.lebreton[-at-]free.fr
// -----------------------------------------------------------------------------

#region " Inclusions "
using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;

using Bdt.Shared.Resources;
using Bdt.Shared.Service;
using Bdt.Shared.Logs;
using System.Collections;
#endregion

namespace Bdt.Shared.Protocol
{

    /// -----------------------------------------------------------------------------
    /// <summary>
    /// Protocole de communication basé sur le remoting .NET
    /// </summary>
    /// -----------------------------------------------------------------------------
    public abstract class GenericRemoting<T> : GenericProtocol where T: IChannel
    {

        #region " Constantes "
        public const string CFG_NAME = "name";
        public const string CFG_PORT_NAME = "portName";
        public const string CFG_PORT = "port";
        #endregion

        #region " Attributs "
        protected T m_clientchannel;
        protected T m_serverchannel;
        #endregion

        #region " Proprietes "
        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Le canal de communication côté serveur
        /// </summary>
        /// -----------------------------------------------------------------------------
        protected abstract T ServerChannel
        {
            get;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Le canal de communication côté client
        /// </summary>
        /// -----------------------------------------------------------------------------
        public abstract T ClientChannel
        {
            get;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// L'URL nécessaire pour se connecter au serveur
        /// </summary>
        /// -----------------------------------------------------------------------------
        protected abstract string ServerURL
        {
            get;
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Le service est-il sécurisé
        /// </summary>
        /// -----------------------------------------------------------------------------
        protected virtual bool IsSecured
        {
            get
            {
                return false;
            }
        }
        #endregion

        #region " Méthodes "
        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Configuration côté client
        /// </summary>
        /// -----------------------------------------------------------------------------
        public override void ConfigureClient()
        {
            Log(string.Format(Strings.CONFIGURING_CLIENT, this.GetType().Name, ServerURL), ESeverity.DEBUG);
            ChannelServices.RegisterChannel(ClientChannel, IsSecured);
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Dé-configuration côté client
        /// </summary>
        /// -----------------------------------------------------------------------------
        public override void UnConfigureClient()
        {
            Log(string.Format(Strings.UNCONFIGURING_CLIENT, this.GetType().Name), ESeverity.DEBUG);
            ChannelServices.UnregisterChannel(ClientChannel);
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Configuration côté serveur
        /// </summary>
        /// <param name="type">le type d'objet à rendre distant</param>
        /// -----------------------------------------------------------------------------
        public override void ConfigureServer(Type type)
        {
            Log(string.Format(Strings.CONFIGURING_SERVER, this.GetType().Name, Port), ESeverity.INFO);
            ChannelServices.RegisterChannel(ServerChannel, IsSecured);
            WellKnownServiceTypeEntry wks = new WellKnownServiceTypeEntry(type, Name, WellKnownObjectMode.Singleton);
            RemotingConfiguration.RegisterWellKnownServiceType(wks);
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Déconfiguration côté serveur
        /// </summary>
        /// -----------------------------------------------------------------------------
        public override void UnConfigureServer()
        {
            Log(string.Format(Strings.UNCONFIGURING_SERVER, this.GetType().Name, Port), ESeverity.INFO);
            ChannelServices.UnregisterChannel(ServerChannel);
            if (ServerChannel is IChannelReceiver)
            {
                ((IChannelReceiver)ServerChannel).StopListening(null);
            }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Retourne une instance de tunnel
        /// </summary>
        /// <returns>une instance de tunnel</returns>
        /// -----------------------------------------------------------------------------
        public override Service.ITunnel GetTunnel()
        {
            return ((ITunnel)Activator.GetObject(typeof(ITunnel), ServerURL));
        }

        public virtual Hashtable CreateClientChannelProperties()
        {
            Hashtable properties = new Hashtable();
            properties.Add(CFG_NAME, string.Format("{0}.Client", Name));
            properties.Add(CFG_PORT_NAME, properties[CFG_NAME]);
            return properties;
        }

        public virtual Hashtable CreateServerChannelProperties()
        {
            Hashtable properties = new Hashtable();
            properties.Add(CFG_NAME, Name);
            properties.Add(CFG_PORT, Port.ToString());
            properties.Add(CFG_PORT_NAME, properties[CFG_NAME]);
            return properties;
        }

        #endregion

    }

}

