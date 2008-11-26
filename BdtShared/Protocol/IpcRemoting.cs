// -----------------------------------------------------------------------------
// BoutDuTunnel
// Sebastien LEBRETON
// sebastien.lebreton[-at-]free.fr
// -----------------------------------------------------------------------------

#region " Inclusions "
using System;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Remoting.Channels;
using System.Collections;
#endregion

namespace Bdt.Shared.Protocol
{

    /// -----------------------------------------------------------------------------
    /// <summary>
    /// Protocole de communication basé sur le remoting .NET et sur le protocole IPC
    /// Exclusivement pour une communication sur la même machine (client/serveur)
    /// </summary>
    /// -----------------------------------------------------------------------------
    public class IpcRemoting : GenericRemoting<IpcChannel>
    {

        #region " Proprietes "
        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Le canal de communication côté client
        /// </summary>
        /// -----------------------------------------------------------------------------
        public override IpcChannel ClientChannel
        {
            get
            {
                if (m_clientchannel == null)
                {
                    m_clientchannel = new IpcChannel(CreateClientChannelProperties(), null, null);
                }
                return m_clientchannel;
            }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Le canal de communication côté serveur
        /// </summary>
        /// -----------------------------------------------------------------------------
        protected override IpcChannel ServerChannel
        {
            get
            {
                if (m_serverchannel == null)
                {
                    m_serverchannel = new IpcChannel(CreateServerChannelProperties(), null, null);
                }
                return m_serverchannel;
            }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// L'URL nécessaire pour se connecter au serveur
        /// </summary>
        /// -----------------------------------------------------------------------------
        protected override string ServerURL
        {
            get
            {
                //return string.Format("ipc://{0}:{1}/{2}", Address, Port, Name);
                return string.Format("ipc://{0}/{0}", Name);
            }
        }

        #endregion

    }

}

