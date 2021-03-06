//
// RosterWidget.cs
//
// Copyright (C) 2008 Eric Butler
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
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;

using Qyoto;

using Synapse.Core;
using Synapse.Core.ExtensionMethods;
using Synapse.ServiceStack;
using Synapse.Services;
using Synapse.Xmpp;
using Synapse.Xmpp.Services;
using Synapse.UI;
using Synapse.UI.Services;
using Synapse.QtClient;
using Synapse.QtClient.Windows;
using Synapse.QtClient.ExtensionNodes;

using jabber;
using jabber.connection;
using jabber.protocol.client;
using jabber.protocol.iq;

using Mono.Rocks;
using Mono.Addins;

namespace Synapse.QtClient.Widgets
{
	public partial class RosterWidget : QWidget
	{
		BookmarkedMUCsModel   m_MucModel;
		RosterAvatarGridModel m_RosterModel;
		QMenu                 m_RosterMenu;
		QMenu                 m_RosterItemMenu;
		QAction               m_ShowOfflineAction;
		QMenu                 m_InviteMenu;
		List<QAction>         m_InviteActions;
		QAction 			  m_ViewProfileAction;
		QAction               m_IMAction;
		QAction               m_GridModeAction;
		QAction               m_ListModeAction;
		QAction               m_ShowTransportsAction;
		QAction               m_EditGroupsAction;
		QAction               m_RemoveAction;
		RosterItem            m_MenuDownItem;
		QIcon                 m_CollapseIcon;
		QIcon                 m_ExpandIcon;
		QMenu                 m_FeedFilterMenu;

		bool m_FeedIsLoaded = false;
		Queue<IActivityFeedItem> m_FeedItemQueue = new Queue<IActivityFeedItem>();
		
		// Map the JS element ID to the ActivityFeedItem
		Dictionary<string, IActivityFeedItem> m_ActivityFeedItems;
		
		public RosterWidget (QWidget parent) : base (parent)
		{
			SetupUi();
			
			var settingsService = ServiceManager.Get<SettingsService>();
			
			m_RosterModel = new RosterAvatarGridModel();
			m_RosterModel.ShowTransports = settingsService.Get<bool>("RosterShowTransports");
			m_RosterModel.ShowOffline = settingsService.Get<bool>("RosterShowOffline");
			rosterGrid.Model = m_RosterModel;
			rosterGrid.ListMode = settingsService.Get<bool>("RosterListMode");
			rosterGrid.ItemActivated += HandleItemActivated;
			rosterGrid.ShowGroupCounts = true;
			rosterGrid.InstallEventFilter(new KeyPressEater(delegate (QKeyEvent evnt) {
				if (!String.IsNullOrEmpty(evnt.Text())) {
					rosterSearchButton.Checked = true;
					friendSearchLineEdit.Text += evnt.Text();
					friendSearchLineEdit.SetFocus();
					return true;
				}
				return false;
			}, this));
	
			if (settingsService.Has("RosterIconSize"))
				rosterGrid.IconSize = settingsService.Get<int>("RosterIconSize");
			
			var accountService = ServiceManager.Get<AccountService>();
			accountService.AccountAdded += HandleAccountAdded;
			accountService.AccountRemoved += HandleAccountRemoved;
			foreach (Account account in accountService.Accounts) {
				HandleAccountAdded(account);
			}
			
			m_ActivityFeedItems = new Dictionary<string, IActivityFeedItem>();
			
			rosterGrid.ContextMenuPolicy = Qt.ContextMenuPolicy.CustomContextMenu;
			
			m_RosterMenu = new QMenu(this);
			QObject.Connect<QAction>(m_RosterMenu, Qt.SIGNAL("triggered(QAction*)"), HandleRosterMenuTriggered);
	
			var rosterViewActionGroup = new QActionGroup(this);
			QObject.Connect<QAction>(rosterViewActionGroup, Qt.SIGNAL("triggered(QAction *)"), RosterViewActionGroupTriggered);
	
			m_GridModeAction = new QAction("View as Grid", this);
			m_GridModeAction.SetActionGroup(rosterViewActionGroup);
			m_GridModeAction.Checkable = true;
			m_RosterMenu.AddAction(m_GridModeAction);
	
			m_ListModeAction = new QAction("View as List", this);
			m_ListModeAction.SetActionGroup(rosterViewActionGroup);
			m_ListModeAction.Checkable = true;
			m_RosterMenu.AddAction(m_ListModeAction);
			
			rosterGrid.ListMode = m_ListModeAction.Checked;
	
			m_RosterMenu.AddSeparator();
			
			m_ShowOfflineAction = new QAction("Show Offline Friends", this);
			m_ShowOfflineAction.Checkable = true;
			m_RosterMenu.AddAction(m_ShowOfflineAction);
			
			m_ShowTransportsAction = new QAction("Show Transports", this);
			m_ShowTransportsAction.Checkable = true;
			m_RosterMenu.AddAction(m_ShowTransportsAction);
	
			m_RosterMenu.AddSeparator();
	
			var sliderAction = new AvatarGridZoomAction<Synapse.UI.RosterItem>(rosterGrid);
			sliderAction.ValueChanged += delegate(int value) {
				rosterGrid.IconSize = value;
				settingsService.Set("RosterIconSize", value);				
			};
			m_RosterMenu.AddAction(sliderAction);
			
			m_InviteActions = new List<QAction>();
			
			m_InviteMenu = new QMenu(this);
			m_InviteMenu.MenuAction().Text = "Invite To";
			m_InviteMenu.AddAction("New Conference...");
	
			m_RosterItemMenu = new QMenu(this);
			QObject.Connect<QAction>(m_RosterItemMenu, Qt.SIGNAL("triggered(QAction*)"), HandleRosterItemMenuTriggered);
			QObject.Connect(m_RosterItemMenu, Qt.SIGNAL("aboutToShow()"), RosterItemMenuAboutToShow);
			QObject.Connect(m_RosterItemMenu, Qt.SIGNAL("aboutToHide()"), RosterItemMenuAboutToHide);
	
			m_ViewProfileAction = new QAction("View Profile", m_RosterItemMenu);
			m_RosterItemMenu.AddAction(m_ViewProfileAction);
			
			m_IMAction = new QAction("IM", m_RosterItemMenu);
			m_RosterItemMenu.AddAction(m_IMAction);
			
			m_RosterItemMenu.AddAction("Send File...");
			m_RosterItemMenu.AddMenu(m_InviteMenu);
			m_RosterItemMenu.AddAction("View History");
			
			foreach (IActionCodon node in AddinManager.GetExtensionNodes("/Synapse/QtClient/Roster/FriendActions")) {
				m_RosterItemMenu.AddAction((QAction)node.CreateInstance(this));
			}
			
			m_RosterItemMenu.AddSeparator();
	
			m_EditGroupsAction = new QAction("Edit Groups", m_RosterItemMenu);
			m_RosterItemMenu.AddAction(m_EditGroupsAction);
	
			m_RemoveAction = new QAction("Remove", m_RosterItemMenu);
			m_RosterItemMenu.AddAction(m_RemoveAction);
			
			friendSearchLineEdit.InstallEventFilter(new KeyPressEater(delegate (QKeyEvent evnt) {
				if (evnt.Key() == (int)Key.Key_Escape) {
					friendSearchLineEdit.Clear();
					rosterSearchButton.Checked = false;
					rosterGrid.SetFocus();
					return true;
				}
				return false;
			}, this));
			
			//QSizeGrip grip = new QSizeGrip(tabWidget);
			//tabWidget.SetCornerWidget(grip, Qt.Corner.BottomRightCorner);
		
			0.UpTo(9).ForEach(num => {
				QAction action = new QAction(this);
				action.Shortcut = new QKeySequence("Alt+" + num.ToString());
				QObject.Connect(action, Qt.SIGNAL("triggered(bool)"), delegate {
					tabWidget.CurrentIndex = num - 1;
				});
				this.AddAction(action);
			});
	
			var jsWindowObject = new SynapseJSObject(this);
			m_ActivityWebView.Page().linkDelegationPolicy = QWebPage.LinkDelegationPolicy.DelegateAllLinks;
			QObject.Connect<QUrl>(m_ActivityWebView, Qt.SIGNAL("linkClicked(QUrl)"), HandleActivityLinkClicked);
			QObject.Connect<bool>(m_ActivityWebView.Page(), Qt.SIGNAL("loadFinished(bool)"), HandleActivityPageLoadFinished);
			QObject.Connect(m_ActivityWebView.Page().MainFrame(), Qt.SIGNAL("javaScriptWindowObjectCleared()"), delegate {
				m_ActivityWebView.Page().MainFrame().AddToJavaScriptWindowObject("Synapse", jsWindowObject);
			});
			m_ActivityWebView.Page().MainFrame().Load("resource:/feed.html");
			
			//friendMucListWebView.Page().MainFrame().Load("resource:/friend-muclist.html");
	
			//quickJoinMucContainer.Hide();
			shoutContainer.Hide();
	
			QObject.Connect(shoutLineEdit, Qt.SIGNAL("textChanged(const QString &)"), delegate {
				shoutCharsLabel.Text = (140 - shoutLineEdit.Text.Length).ToString();
			});
			
			QObject.Connect(shoutLineEdit, Qt.SIGNAL("returnPressed()"), delegate {
				SendShout();
			});
	
			QVBoxLayout layout = new QVBoxLayout(m_AccountsContainer);
			layout.Margin = 0;
			m_AccountsContainer.SetLayout(layout);
	
			m_MucModel = new BookmarkedMUCsModel();
			mucTree.SetModel(m_MucModel);
	
			friendSearchContainer.Hide();
			
			rosterViewButton.icon  = new QIcon(new QPixmap("resource:/view-grid.png"));
			rosterSearchButton.icon = new QIcon(new QPixmap("resource:/simple-search.png"));
			addFriendButton.icon = new QIcon(new QPixmap("resource:/simple-add.png"));
			addMucBookmarkButton.icon = new QIcon(new QPixmap("resource:/simple-add.png"));
			feedFilterButton.icon = new QIcon(new QPixmap("resource:/simple-search.png"));
				
			m_CollapseIcon = new QIcon(new QPixmap("resource:/collapse.png"));
			m_ExpandIcon = new QIcon(new QPixmap("resource:/expand.png"));
			toggleJoinMucButton.icon = m_CollapseIcon;
				
			UpdateOnlineCount();
	
			var shoutService = ServiceManager.Get<ShoutService>();
			shoutService.HandlerAdded += HandleShoutHandlerAdded;
			shoutService.HandlerRemoved += HandleShoutHandlerRemoved;
			if (shoutService.Handlers.Count() > 0) {
				foreach (IShoutHandler handler in shoutService.Handlers) {
					HandleShoutHandlerAdded(handler);
				}
			} else {
				shoutHandlersBox.Hide();
			}
	
			m_FeedFilterMenu = new QMenu(this);
			
			QObject.Connect(m_FeedFilterMenu, Qt.SIGNAL("triggered(QAction*)"), delegate (QAction action) {
				string js = Util.CreateJavascriptCall("ActivityFeed.setCategoryVisibility", action.Text.ToLower().Replace(" ", "-"), action.Checked);
				m_ActivityWebView.Page().MainFrame().EvaluateJavaScript(js);
			});
			
			var feedService = ServiceManager.Get<ActivityFeedService>();
			feedService.NewItem += delegate (IActivityFeedItem item) {
				lock (m_FeedItemQueue) {
					if (!m_FeedIsLoaded) {
						m_FeedItemQueue.Enqueue(item);
					} else {
						AddActivityFeedItem(item);
					}
				}
			};
			feedService.CategoryAdded += delegate (string category) {
				QApplication.Invoke(delegate {
					HandleCategoryAdded(category);
				});
			};
			foreach (string category in feedService.Categories) {
				HandleCategoryAdded(category);
			}
		}
	
		public new void Show ()
		{
			base.Show();
			rosterGrid.SetFocus();
		}
	
		public int AccountsCount {
			get {
				return m_AccountsContainer.Layout().Count();
			}
		}
		
		public void AddAccount(Account account)
		{
			AccountStatusWidget widget = new AccountStatusWidget(account, this, (MainWindow)base.TopLevelWidget());
			m_AccountsContainer.Layout().AddWidget(widget);
			widget.Show();
		}
	
		public void RemoveAccount(Account account)
		{
			for (int x = 0; x < m_AccountsContainer.Layout().Count(); x++) {
				var item = m_AccountsContainer.Layout().ItemAt(x);
				if (item is QWidgetItem) {					
					AccountStatusWidget widget = (AccountStatusWidget)((QWidgetItem)item).Widget();
					if (widget.Account == account) {
						m_AccountsContainer.Layout().RemoveWidget(widget);
						widget.SetParent(null);
						break;
					}
				}
			}
		}
		
		public void AddActivityFeedItem (IActivityFeedItem item)
		{
			QApplication.Invoke(delegate {					
				string accountJid = (item is XmppActivityFeedItem && ((XmppActivityFeedItem)item).Account != null) ? ((XmppActivityFeedItem)item).Account.Jid.Bare : null;
				string fromJid = (item is XmppActivityFeedItem) ? ((XmppActivityFeedItem)item).FromJid : null;
				string content = Util.Linkify(item.Content);
				string js = Util.CreateJavascriptCall("ActivityFeed.addItem", accountJid, item.Type, item.AvatarUrl, 
			                                      	  fromJid, item.FromName, item.FromUrl, item.ActionItem, content, 
				                                      item.ContentUrl);
				var result = m_ActivityWebView.Page().MainFrame().EvaluateJavaScript(js);
				if (!result.IsNull()) {
					m_ActivityFeedItems.Add(result.ToString(), item);
				}
			});
		}
	
		public RosterItem SelectedItem {
			get {
				return m_MenuDownItem;
			}
		}
		
		void SendShout ()
		{
			if (!String.IsNullOrEmpty(shoutLineEdit.Text)) {
	
				List<IShoutHandler> selectedHandlers = new List<IShoutHandler>();
				
				for (int x = 0; x < shoutHandlersContainer.Layout().Count(); x++) {
					var item = shoutHandlersContainer.Layout().ItemAt(x);
					if (item is QWidgetItem) {
						var check = ((ShoutHandlerCheckBox)((QWidgetItem)item).Widget());
						if (check.Checked) {
							selectedHandlers.Add(check.Handler);
						}
					}
				}
				
				try {
					var service = ServiceManager.Get<ShoutService>();
					service.Shout(shoutLineEdit.Text, selectedHandlers.ToArray());
					shoutLineEdit.Clear();
				} catch (UserException ex) {
					QMessageBox.Critical(base.TopLevelWidget(), "Synapse", ex.Message);
				}
			}
		}
		
		void HandleItemActivated (AvatarGrid<RosterItem> grid, RosterItem item)
		{
			Gui.TabbedChatsWindow.StartChat(item.Account, item.Item.JID);
		}
	
		void HandleAccountAdded (Account account)
		{
			account.Client.OnPresence += HandleOnPresence;
			account.ConnectionStateChanged += HandleConnectionStateChanged;
	
			QApplication.Invoke(delegate {
				UpdateOnlineCount();
			});
		}
	
		void HandleAccountRemoved (Account account)
		{
			account.Client.OnPresence -= HandleOnPresence;
			account.ConnectionStateChanged -= HandleConnectionStateChanged;
			
			QApplication.Invoke(delegate {
				UpdateOnlineCount();
			});
		}
	
		void HandleOnPresence (object o, Presence pres)
		{
			QApplication.Invoke(delegate {
				UpdateOnlineCount();
			});
		}
	
		void HandleConnectionStateChanged (Account account)
		{
			QApplication.Invoke(delegate {
				UpdateOnlineCount();
			});
		}
	
		void UpdateOnlineCount ()
		{
			var accountService = ServiceManager.Get<AccountService>();
			int num = accountService.Accounts.Sum(account => account.NumOnlineFriends);
			statsLabel.Text = String.Format("{0} friends online", num);
		}
		
		void HandleShoutHandlerAdded (IShoutHandler handler)
		{
			QApplication.Invoke(delegate {
				QCheckBox check = new ShoutHandlerCheckBox(handler, shoutHandlersContainer);
				check.Checked = true;
				shoutHandlersContainer.Layout().AddWidget(check);
				
				shoutHandlersBox.Show();
			});
		}
		
		void HandleShoutHandlerRemoved (IShoutHandler handler)
		{
			QApplication.Invoke(delegate {
				for (int x = 0; x < shoutHandlersContainer.Layout().Count(); x++) {
					var item = shoutHandlersContainer.Layout().ItemAt(x);
					if (item is QWidgetItem) {
						var check = ((ShoutHandlerCheckBox)((QWidgetItem)item).Widget());
						if (check.Handler == handler) {
							shoutHandlersContainer.Layout().RemoveWidget(check);
							check.SetParent(null);
							check.Dispose();
							break;
						}
					}
				}
				
				var shoutService = ServiceManager.Get<ShoutService>();
				if (shoutService.Handlers.Count() == 0) {
					shoutHandlersBox.Hide();
				}
			});
		}
		
		#region Private Slots
		[Q_SLOT]
		void on_sendShoutButton_clicked()
		{
			SendShout();
		}
		
		[Q_SLOT]
		void on_m_ShoutButton_toggled(bool active)
		{
		}
		
		[Q_SLOT]
		void on_feedFilterButton_clicked()
		{
			var buttonPos = feedFilterButton.MapToGlobal(new QPoint(0, feedFilterButton.Height()));
			m_FeedFilterMenu.Popup(buttonPos);
		}
		
		[Q_SLOT]
		void on_toggleJoinMucButton_clicked()
		{
			if (joinMucContainer.IsVisible()) {
				joinMucContainer.Hide();
				toggleJoinMucButton.icon = m_ExpandIcon;
			} else {
				joinMucContainer.Show();
				toggleJoinMucButton.icon = m_CollapseIcon;
			}			
		}
		 
		[Q_SLOT]
		void on_m_JoinChatButton_clicked()
		{
			Account selectedAccount = Gui.ShowAccountSelectMenu(m_JoinChatButton);
			if (selectedAccount != null) {
				JID jid = null;
				string nick = (!String.IsNullOrEmpty(mucNicknameLineEdit.Text)) ? mucNicknameLineEdit.Text : selectedAccount.ConferenceManager.DefaultNick;
				if (JID.TryParse(String.Format("{0}@{1}/{2}", mucRoomLineEdit.Text, mucServerLineEdit.Text, nick), out jid)) {
					if (!String.IsNullOrEmpty(jid.User) && !String.IsNullOrEmpty(jid.Server)) {
						try {
							selectedAccount.JoinMuc(jid, mucPasswordLineEdit.Text);
						} catch (UserException ex) {
							QMessageBox.Critical(this.TopLevelWidget(), "Synapse Error", ex.Message);
						}
					} else {
						QMessageBox.Critical(null, "Synapse", "Invalid JID");
					}
				} else {
					QMessageBox.Critical(this.TopLevelWidget(), "Synapse Error", "Invalid conference room");
				}
			}
		}
	
		[Q_SLOT]
		void on_mucTree_activated(QModelIndex index)
		{
			if (index.IsValid()) {
				if (index.InternalPointer() is BookmarkConference) {
					Account account = (Account)index.Parent().InternalPointer();
					BookmarkConference conf = (BookmarkConference)index.InternalPointer();
					try {
						account.JoinMuc(conf.JID, conf.Password);
					} catch (UserException e) {
						QMessageBox.Critical(this.TopLevelWidget(), "Synapse", e.Message);
					}
				}
			}
		}
	
		[Q_SLOT]
		void on_rosterGrid_customContextMenuRequested(QPoint point)
		{
			m_MenuDownItem = rosterGrid.HoverItem;
			if (m_MenuDownItem != null) {
				m_InviteActions.ForEach(a => m_InviteMenu.RemoveAction(a));
				if (m_MenuDownItem.Account.ConferenceManager.Count > 0) {
					m_InviteActions.Add(m_InviteMenu.AddSeparator());
					foreach (var conference in m_MenuDownItem.Account.ConferenceManager.Rooms) {
						QAction action = m_InviteMenu.AddAction(conference.JID);
						m_InviteActions.Add(action);
					}
				}
				m_RosterItemMenu.Popup(rosterGrid.MapToGlobal(point));
			}
		}
	
		void HandleRosterItemMenuTriggered (QAction action)
		{
			// FIXME: Actions should be handled in the controller.
			
			if (m_MenuDownItem == null)
				return;
			
			if (action == m_ViewProfileAction) {
				var window = new ProfileWindow(m_MenuDownItem.Account, m_MenuDownItem.Item.JID);
				window.Show();
			} else if (action == m_IMAction) {
				Gui.TabbedChatsWindow.StartChat(m_MenuDownItem.Account, m_MenuDownItem.Item.JID);
			} else if (m_InviteActions.Contains(action)) {
				foreach (Room room in m_MenuDownItem.Account.ConferenceManager.Rooms) {
					if (room.JID.Bare == action.Text) {
						Console.WriteLine("Invite: " + m_MenuDownItem.Item.JID);
						room.Invite(m_MenuDownItem.Item.JID, String.Empty);
						return;
					}
				}
			} else if (action == m_EditGroupsAction) {
				var win = new EditGroupsWindow(m_MenuDownItem.Account, m_MenuDownItem.Item);
				win.Show();
			} else if (action == m_RemoveAction) {
				if (QMessageBox.Question(this.TopLevelWidget(), "Synapse", "Are you sure you want to remove this friend?", (uint)QMessageBox.StandardButton.Yes | (uint)QMessageBox.StandardButton.No) == QMessageBox.StandardButton.Yes) {
					m_MenuDownItem.Account.RemoveRosterItem(m_MenuDownItem.Item.JID);
				}
			}
		}
		
		void HandleRosterMenuTriggered (QAction action)
		{
			var settingsService = ServiceManager.Get<SettingsService>();
			if (action == m_ShowOfflineAction) {
				m_RosterModel.ShowOffline = action.Checked;
				settingsService.Set("RosterShowOffline", m_RosterModel.ShowOffline);
			} else if (action == m_ShowTransportsAction) {
				m_RosterModel.ShowTransports = action.Checked;
				settingsService.Set("RosterShowTransports", m_RosterModel.ShowTransports);
			}
		}
	
		void RosterViewActionGroupTriggered (QAction action)
		{
			if (action == m_ListModeAction) {
				rosterGrid.ListMode = action.Checked;
				rosterViewButton.icon  = new QIcon(new QPixmap("resource:/view-list.png"));
			} else if (action == m_GridModeAction) {
				rosterGrid.ListMode = !action.Checked;
				rosterViewButton.icon  = new QIcon(new QPixmap("resource:/view-grid.png"));
			}
			var settingsService = ServiceManager.Get<SettingsService>();
			settingsService.Set("RosterListMode", m_ListModeAction.Checked);
		}
		
		[Q_SLOT]
		void on_friendSearchLineEdit_textChanged ()
		{
			m_RosterModel.TextFilter = friendSearchLineEdit.Text;
		}
	
		void HandleActivityPageLoadFinished (bool ok)
		{
			if (m_ActivityWebView.Url.ToString() != "resource:/feed.html")
				return;
			
			if (!ok)
				throw new Exception("Failed to load activity feed html!");
	
			// FIXME: This is very strange.
			while (m_ActivityWebView.Page().MainFrame().EvaluateJavaScript("ActivityFeed.loaded").ToBool() != true) {
				Console.WriteLine("Failed to load activity feed, trying again!");
				m_ActivityWebView.Page().MainFrame().Load("resource:/feed.html");
				return;
			}
	
			var feedService = ServiceManager.Get<ActivityFeedService>();
			feedService.TemplateAdded += HandleTemplateAdded;
			foreach (var template in feedService.Templates.Values) {
				HandleTemplateAdded(template);
			}

			lock (m_FeedItemQueue) {
				m_FeedIsLoaded = true;
				while (m_FeedItemQueue.Count > 0) {
					AddActivityFeedItem(m_FeedItemQueue.Dequeue());
				}
			}
		}
	
		void HandleTemplateAdded (ActivityFeedItemTemplate template)
		{
			QApplication.Invoke(delegate {
				string category = (template.Category == null) ? null : template.Category.ToLower().Replace(" ", "-");
				string js = Util.CreateJavascriptCall("ActivityFeed.addTemplate", template.Name, category, 
								      template.SingularText, template.PluralText, template.IconUrl, 
								      template.Actions);
				var ret = m_ActivityWebView.Page().MainFrame().EvaluateJavaScript(js);
				if (ret.IsNull() || !ret.ToBool()) {
					throw new Exception("Failed to add template!\n" + js);
				}
			});
		}

		void HandleCategoryAdded (string category)
		{
			QAction action = new QAction(category, m_FeedFilterMenu);
			action.Checkable = true;
			action.Checked = true;
			m_FeedFilterMenu.AddAction(action);
		}
		
		void HandleActivityLinkClicked (QUrl url)
		{
			try {
				Uri uri = new Uri(url.ToString());
				if (uri.Scheme == "http" || uri.Scheme == "https") {
					Util.Open(uri.ToString());
				} else {
					if (uri.Scheme == "xmpp") {
						JID jid = new JID(uri.AbsolutePath);
						var query = XmppUriQueryInfo.ParseQuery(uri.Query);
						switch (query.QueryType) {
						case "message":
							// FIXME: Should not ask which account to use, should use whichever account generated the event.
							var account = Gui.ShowAccountSelectMenu(this);
							if (account != null)
								Gui.TabbedChatsWindow.StartChat(account, jid);
							break;
						default:
							throw new NotSupportedException("Unsupported query type: " + query.QueryType);
						}
					} else if (uri.Scheme == "activity-item") {
						string itemId = uri.AbsolutePath;
						string action = uri.Query.Substring(1);
						m_ActivityFeedItems[itemId].TriggerAction(action);
					}
				}
			} catch (Exception ex) {
				Console.Error.WriteLine(ex);
				QMessageBox.Critical(null, "Synapse Error", ex.Message);
			}
		}
	
		[Q_SLOT]
		void on_addFriendButton_clicked ()
		{
			Account account = Gui.ShowAccountSelectMenu(addFriendButton);
			if (account != null) {
				AddFriendWindow window = new AddFriendWindow(account);
				window.Show();
			}
		}
	
		void RosterItemMenuAboutToShow ()
		{
			rosterGrid.SuppressTooltips = true;

			foreach (var action in m_RosterItemMenu.Actions()) {
				if (action is IUpdateableAction) {
					((IUpdateableAction)action).Update();
				}
			}
		}
	
		void RosterItemMenuAboutToHide ()
		{
			rosterGrid.SuppressTooltips = false;
		}
	
		[Q_SLOT]
		void on_rosterSearchButton_toggled (bool active)
		{
			friendSearchContainer.SetVisible(active);
			m_RosterModel.TextFilter = active ? friendSearchLineEdit.Text : String.Empty;
		}
	
		[Q_SLOT]
		void on_rosterViewButton_clicked ()
		{
			m_ShowOfflineAction.Checked = m_RosterModel.ShowOffline;
			m_ShowTransportsAction.Checked = m_RosterModel.ShowTransports;
	
			var buttonPos = rosterViewButton.MapToGlobal(new QPoint(0, rosterViewButton.Height()));
			m_RosterMenu.Popup(buttonPos);
		}
		#endregion
	
		class ShoutHandlerCheckBox : QCheckBox
		{
			IShoutHandler m_Handler;
			
			public ShoutHandlerCheckBox (IShoutHandler handler, QWidget parent) : base (handler.Name, parent)
			{
				m_Handler = handler;
			}
	
			public IShoutHandler Handler {
				get {
					return m_Handler;
				}	
			}
		}
	}
}
