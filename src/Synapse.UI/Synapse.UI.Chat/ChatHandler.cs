//
// ChatHandler.cs
// 
// Copyright (C) 2009 Eric Butler
//
// Authors:
//   Eric Butler <eric@extremeboredom.net>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Xml;
using Synapse.Xmpp;
using jabber;
using jabber.protocol.client;

namespace Synapse.UI.Chat
{
	public class ChatHandler : AbstractChatHandler
	{
		JID m_Jid;
		bool m_IsMucUser;
		
		public ChatHandler (Account account, bool isMucUser, JID jid)
			: base (account)
		{
			m_Jid = jid;
			m_IsMucUser = isMucUser;
			
			base.Account.ConnectionStateChanged += HandleConnectionStateChanged;
			base.Ready = (base.Account.ConnectionState == AccountConnectionState.Connected);
			
			base.AppendStatus(String.Format("Conversation with {0}.", jid.ToString()));
		}
		
		public JID Jid {
			get {
				return m_Jid;
			}
		}

		public string Resource {
			get;
			set;
		}
		
		public bool IsMucMessage {
			get {
				return m_IsMucUser;
			}
		}
		
		public void SetPresence (Presence presence)
		{
			string message = null;
			string fromName = base.Account.GetDisplayName(presence.From);
			if (!String.IsNullOrEmpty(presence.Status)) {
				message = String.Format("{0} ({1}) is now {2}: {3}.", fromName, Helper.GetResourceDisplay(presence), Helper.GetPresenceDisplay(presence), presence.Status);
			} else {
				message = String.Format("{0} ({1}) is now {2}.", fromName, Helper.GetResourceDisplay(presence), Helper.GetPresenceDisplay(presence));
			}
			
			base.AppendStatus(message);
		}

		public override void Send (string html)
		{	
			if (!String.IsNullOrEmpty(html)) {
				Message message = new Message(base.Account.Client.Document);
				message.Type = MessageType.chat;
				if (IsMucMessage)
					message.To = this.Jid;
				else
					message.To = new JID(m_Jid.User, m_Jid.Server, Resource);
				message.Html = html;

				var activeElem = base.Account.Client.Document.CreateElement("active");
				activeElem.SetAttribute("xmlns", "http://jabber.org/protocol/chatstates");
				message.AppendChild(activeElem);

				// FIXME: For some reason this blocks on large messages.
				base.Account.Client.Write(message);

				base.AppendMessage(false, message);
			}			
		}

		public override void Send (XmlElement contentElement)
		{
			Message message = new Message(base.Account.Client.Document);
			message.Type = MessageType.chat;
			message.To = m_Jid;

			message.AppendChild(contentElement);

			var activeElem = base.Account.Client.Document.CreateElement("active");
			activeElem.SetAttribute("xmlns", "http://jabber.org/protocol/chatstates");
			message.AppendChild(activeElem);

			base.Account.Client.Write(message);
			base.AppendMessage(false, message);
		}

		public override void Dispose ()
		{
			base.Account.ConnectionStateChanged -= HandleConnectionStateChanged;
		}

		void HandleConnectionStateChanged(Account account)
		{
			if (account.ConnectionState == AccountConnectionState.Connected) {
				AppendStatus("You are now online.");
				base.Ready = true;
			} else if (account.ConnectionState == AccountConnectionState.Disconnected) {
				AppendStatus("You are now offline.");
				base.Ready = false;
			}
		}
	}
}
