// -----------------------------------------------------------------------------
// BoutDuTunnel
// Sebastien LEBRETON
// sebastien.lebreton[-at-]free.fr
// -----------------------------------------------------------------------------

#region " Inclusions "
using System;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels;
#endregion

namespace Bdt.Shared.Protocol
{

    /// -----------------------------------------------------------------------------
    /// <summary>
    /// Protocole de communication basé sur le remoting .NET et sur le protocole HTTP
    /// Utilise un formateur SOAP pour les données
    /// </summary>
    /// -----------------------------------------------------------------------------
    public class HttpSoapRemoting : GenericHttpRemoting
    {

        #region " Proprietes "
        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Le canal de communication côté client
        /// </summary>
        /// -----------------------------------------------------------------------------
        public override HttpChannel ClientChannel
        {
            get
            {
                if (m_clientchannel == null)
                {
                    m_clientchannel = new HttpChannel(CreateClientChannelProperties(), new SoapClientFormatterSinkProvider(), null);
                }
                return m_clientchannel;
            }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        /// Le canal de communication côté serveur
        /// </summary>
        /// -----------------------------------------------------------------------------
        protected override HttpChannel ServerChannel
        {
            get
            {
                if (m_serverchannel == null)
                {
                    m_serverchannel = new HttpChannel(CreateServerChannelProperties(), new SoapClientFormatterSinkProvider(), new SoapServerFormatterSinkProvider());
                }
                return m_serverchannel;
            }
        }
        #endregion

    }

}

