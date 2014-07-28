﻿#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Controls;
using Hearthstone_Deck_Tracker.Hearthstone;
using Card = Hearthstone_Deck_Tracker.Hearthstone.Card;

#endregion

namespace Hearthstone_Deck_Tracker
{
	/// <summary>
	///     Interaction logic for OverlayWindow.xaml
	/// </summary>
	public partial class OverlayWindow
	{
		private readonly List<HearthstoneTextBlock> _cardLabels;
		private readonly List<HearthstoneTextBlock> _cardMarkLabels;
		private readonly List<StackPanel> _stackPanelsMarks;
		private readonly Config _config;
		private readonly int _customHeight;
		private readonly int _customWidth;
		//private readonly Game _game;
		private readonly int _offsetX;
		private readonly int _offsetY;
		private readonly User32.MouseInput _mouseInput;
		private int _cardCount;
		private int _opponentCardCount;
		private string _lastSecretsClass;
		private bool _needToRefreshSecrets;
		private bool _playerCardsHidden;
		private bool _opponentCardsHidden;

		public OverlayWindow(Config config)
		{
			InitializeComponent();
			_config = config;
			//_game = game;

			_mouseInput = new User32.MouseInput();
			_mouseInput.Click += MouseInputOnClick;

			ListViewPlayer.ItemsSource = Game.IsUsingPremade ? Game.PlayerDeck : Game.PlayerDrawn;
			ListViewOpponent.ItemsSource = Game.OpponentCards;
			Scaling = 1.0;
			OpponentScaling = 1.0;
			ShowInTaskbar = _config.ShowInTaskbar;
			if (_config.VisibleOverlay)
			{
				Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#4C0000FF");
			}
			_offsetX = _config.OffsetX;
			_offsetY = _config.OffsetY;
			_customWidth = _config.CustomWidth;
			_customHeight = _config.CustomHeight;

			_cardLabels = new List<HearthstoneTextBlock>
				{
					LblCard0,
					LblCard1,
					LblCard2,
					LblCard3,
					LblCard4,
					LblCard5,
					LblCard6,
					LblCard7,
					LblCard8,
					LblCard9,
				};
			_cardMarkLabels = new List<HearthstoneTextBlock>
				{
					LblCardMark0,
					LblCardMark1,
					LblCardMark2,
					LblCardMark3,
					LblCardMark4,
					LblCardMark5,
					LblCardMark6,
					LblCardMark7,
					LblCardMark8,
					LblCardMark9,
				};
			_stackPanelsMarks = new List<StackPanel>
				{
					Marks0,
					Marks1,
					Marks2,
					Marks3,
					Marks4,
					Marks5,
					Marks6,
					Marks7,
					Marks8,
					Marks9,
				};

			UpdateScaling();
		}

		private void MouseInputOnClick(object sender, EventArgs eventArgs)
		{
			if (!User32.IsForegroundWindow("Hearthstone")) return;

			HideCardsWhenFriendsListOpen();

			GrayOutSecrets();

		}

		private async void HideCardsWhenFriendsListOpen()
		{
			var leftPanel = Canvas.GetLeft(StackPanelOpponent) < 200 ? StackPanelOpponent : StackPanelPlayer;
			if (leftPanel != null && !Config.Instance.HideDecksInOverlay)
			{
				var checkForFriendsList = true;
				if (leftPanel.Equals(StackPanelPlayer) && Config.Instance.HidePlayerCards)
					checkForFriendsList = false;
				else if (leftPanel.Equals(StackPanelOpponent) && Config.Instance.HideOpponentCards)
					checkForFriendsList = false;

				if (checkForFriendsList)
				{
					if (await Helper.FriendsListOpen())
					{
						var needToHide = Canvas.GetTop(leftPanel) + leftPanel.ActualHeight > Height*0.3;
						if (needToHide)
						{
							leftPanel.Visibility = Visibility.Collapsed;
							if (leftPanel.Equals(StackPanelPlayer))
								_playerCardsHidden = true;
							else
								_opponentCardsHidden = true;
						}
					}
					else if (leftPanel.Visibility == Visibility.Collapsed)
					{
						leftPanel.Visibility = Visibility.Visible;
						if (leftPanel.Equals(StackPanelPlayer))
							_playerCardsHidden = false;
						else
							_opponentCardsHidden = false;
					}
				}
			}
		}

		private void GrayOutSecrets()
		{
			if (ToolTipCard.Visibility == Visibility.Visible)
			{
				var card = ToolTipCard.DataContext as Card;
				if (card == null) return;

				// 1: normal, 0: grayed out
				card.Count = card.Count == 0 ? 1 : 0;


				//reload secrets panel
				var cards = StackPanelSecrets.Children.OfType<Controls.Card>().Select(c => c.DataContext).OfType<Card>().ToList();

				StackPanelSecrets.Children.Clear();
				foreach (var c in cards)
				{
					var cardObj = new Controls.Card();
					cardObj.SetValue(DataContextProperty, c);
					StackPanelSecrets.Children.Add(cardObj);
				}

				//reset secrets when new secret is played
				_needToRefreshSecrets = true;
			}
		}

		public static double Scaling { get; set; }
		public static double OpponentScaling { get; set; }

		public void SortViews()
		{
			Helper.SortCardCollection(ListViewPlayer.ItemsSource, _config.CardSortingClassFirst);
			Helper.SortCardCollection(ListViewOpponent.ItemsSource, _config.CardSortingClassFirst);
		}

		private void SetOpponentCardCount(int cardCount, int cardsLeftInDeck)
		{
			//previous cardcout > current -> opponent played -> resort list
			if (_opponentCardCount > cardCount)
			{
				Helper.SortCardCollection(ListViewOpponent.ItemsSource, _config.CardSortingClassFirst);
			}
			_opponentCardCount = cardCount;

			LblOpponentCardCount.Text = "Hand: " + cardCount;
			LblOpponentDeckCount.Text = "Deck: " + cardsLeftInDeck;


			if (cardsLeftInDeck <= 0) return;

			var handWithoutCoin = cardCount - (Game.OpponentHasCoin ? 1 : 0);


			var holdingNextTurn2 =
				Math.Round(100.0f * Helper.DrawProbability(2, (cardsLeftInDeck + handWithoutCoin), handWithoutCoin + 1), 2);
			var drawNextTurn2 = Math.Round(200.0f / cardsLeftInDeck, 2);
			LblOpponentDrawChance2.Text = "[2]: " + holdingNextTurn2 + "% / " + drawNextTurn2 + "%";

			var holdingNextTurn =
				Math.Round(100.0f * Helper.DrawProbability(1, (cardsLeftInDeck + handWithoutCoin), handWithoutCoin + 1), 2);
			var drawNextTurn = Math.Round(100.0f / cardsLeftInDeck, 2);
			LblOpponentDrawChance1.Text = "[1]: " + holdingNextTurn + "% / " + drawNextTurn + "%";
		}

		private void SetCardCount(int cardCount, int cardsLeftInDeck)
		{
			//previous < current -> draw
			if (_cardCount < cardCount)
			{
				Helper.SortCardCollection(ListViewPlayer.ItemsSource, _config.CardSortingClassFirst);
			}
			_cardCount = cardCount;
			LblCardCount.Text = "Hand: " + cardCount;
			LblDeckCount.Text = "Deck: " + cardsLeftInDeck;

			if (cardsLeftInDeck <= 0) return;

			LblDrawChance2.Text = "[2]: " + Math.Round(200.0f / cardsLeftInDeck, 2) + "%";
			LblDrawChance1.Text = "[1]: " + Math.Round(100.0f / cardsLeftInDeck, 2) + "%";
		}

		public void ShowOverlay(bool enable)
		{
			if (enable)
				Show();
			else Hide();
		}

		private void SetRect(int top, int left, int width, int height)
		{
			Top = top + _offsetY;
			Left = left + _offsetX;
			Width = (_customWidth == -1) ? width : _customWidth;
			Height = (_customHeight == -1) ? height : _customHeight;
			CanvasInfo.Width = (_customWidth == -1) ? width : _customWidth;
			CanvasInfo.Height = (_customHeight == -1) ? height : _customHeight;
		}

		private void ReSizePosLists()
		{
			//player
			if (((Height * _config.PlayerDeckHeight / (_config.OverlayPlayerScaling / 100) / 100) -
				 (ListViewPlayer.Items.Count * 35 * Scaling)) < 1 || Scaling < 1)
			{
				var previousScaling = Scaling;
				Scaling = (Height * _config.PlayerDeckHeight / (_config.OverlayPlayerScaling / 100) / 100) /
						  (ListViewPlayer.Items.Count * 35);
				if (Scaling > 1)
					Scaling = 1;

				if (previousScaling != Scaling)
					ListViewPlayer.Items.Refresh();
			}

			Canvas.SetTop(StackPanelPlayer, Height * _config.PlayerDeckTop / 100);
			Canvas.SetLeft(StackPanelPlayer,
						   Width * _config.PlayerDeckLeft / 100 -
						   StackPanelPlayer.ActualWidth * _config.OverlayPlayerScaling / 100);

			//opponent
			if (((Height * _config.OpponentDeckHeight / (_config.OverlayOpponentScaling / 100) / 100) -
				 (ListViewOpponent.Items.Count * 35 * OpponentScaling)) < 1 || OpponentScaling < 1)
			{
				var previousScaling = OpponentScaling;
				OpponentScaling = (Height * _config.OpponentDeckHeight / (_config.OverlayOpponentScaling / 100) / 100) /
								  (ListViewOpponent.Items.Count * 35);
				if (OpponentScaling > 1)
					OpponentScaling = 1;

				if (previousScaling != OpponentScaling)
					ListViewOpponent.Items.Refresh();
			}


			Canvas.SetTop(StackPanelOpponent, Height * _config.OpponentDeckTop / 100);
			Canvas.SetLeft(StackPanelOpponent, Width * _config.OpponentDeckLeft / 100);

			//Secrets
			Canvas.SetTop(StackPanelSecrets, Height * _config.SecretsTop / 100);
			Canvas.SetLeft(StackPanelSecrets, Width * _config.SecretsLeft / 100);

			// Timers
			Canvas.SetTop(LblTurnTime,
						  Height * _config.TimersVerticalPosition / 100 - 5);
			Canvas.SetLeft(LblTurnTime, Width * _config.TimersHorizontalPosition / 100);

			Canvas.SetTop(LblOpponentTurnTime,
						  Height * _config.TimersVerticalPosition / 100 -
						  _config.TimersVerticalSpacing);
			Canvas.SetLeft(LblOpponentTurnTime,
						   (Width * _config.TimersHorizontalPosition / 100) + _config.TimersHorizontalSpacing);

			Canvas.SetTop(LblPlayerTurnTime,
						  Height * _config.TimersVerticalPosition / 100 +
						  _config.TimersVerticalSpacing);
			Canvas.SetLeft(LblPlayerTurnTime,
						   Width * _config.TimersHorizontalPosition / 100 + _config.TimersHorizontalSpacing);


			Canvas.SetTop(LblGrid, Height * 0.03);


			var ratio = Width / Height;
			LblGrid.Width = ratio < 1.5 ? Width * 0.3 : Width * 0.15 * (ratio / 1.33);
		}

		private void Window_SourceInitialized_1(object sender, EventArgs e)
		{
			IntPtr hwnd = new WindowInteropHelper(this).Handle;
			User32.SetWindowExTransparent(hwnd);
		}

		public void Update(bool refresh)
		{
			if (refresh)
			{
				ListViewPlayer.Items.Refresh();
				ListViewOpponent.Items.Refresh();
				Topmost = false;
				Topmost = true;
				Logger.WriteLine("Refreshed overlay topmost status");
			}


			var handCount = Game.OpponentHandCount;
			if (handCount < 0) handCount = 0;
			if (handCount > 10) handCount = 10;
			//offset label-grid based on handcount
			Canvas.SetLeft(LblGrid, Width / 2 - LblGrid.ActualWidth / 2 - Width * 0.002 * handCount);

			var labelDistance = LblGrid.Width / (handCount + 1);

			for (int i = 0; i < handCount; i++)
			{
				var offset = labelDistance * Math.Abs(i - handCount / 2);
				offset = offset * offset * 0.0015;

				if (handCount % 2 == 0)
				{
					if (i < handCount / 2 - 1)
					{
						//even hand count -> both middle labels at same height
						offset = labelDistance * Math.Abs(i - (handCount / 2 - 1));
						offset = offset * offset * 0.0015;
						_stackPanelsMarks[i].Margin = new Thickness(0, -offset, 0, 0);
					}
					else if (i > handCount / 2)
					{
						_stackPanelsMarks[i].Margin = new Thickness(0, -offset, 0, 0);
					}
					else
					{
						var left = (handCount == 2 && i == 0) ? Width * 0.02 : 0;
						_stackPanelsMarks[i].Margin = new Thickness(left, 0, 0, 0);
					}
				}
				else
				{
					_stackPanelsMarks[i].Margin = new Thickness(0, -offset, 0, 0);
				}

				if (!_config.HideOpponentCardAge)
				{
					_cardLabels[i].Text = Game.OpponentHandAge[i].ToString();
					_cardLabels[i].Visibility = Visibility.Visible;
				}
				else
				{
					_cardLabels[i].Visibility = Visibility.Collapsed;
				}

				if (!_config.HideOpponentCardMarks)
				{
					_cardMarkLabels[i].Text = ((char)Game.OpponentHandMarks[i]).ToString();
					_cardMarkLabels[i].Visibility = Visibility.Visible;
				}
				else
				{
					_cardMarkLabels[i].Visibility = Visibility.Collapsed;
				}

				_stackPanelsMarks[i].Visibility = Visibility.Visible;
			}
			for (int i = handCount; i < 10; i++)
			{
				_stackPanelsMarks[i].Visibility = Visibility.Collapsed;
			}

			StackPanelPlayer.Opacity = _config.PlayerOpacity / 100;
			StackPanelOpponent.Opacity = _config.OpponentOpacity / 100;
			Opacity = _config.OverlayOpacity / 100;

			if(!_playerCardsHidden)
				StackPanelPlayer.Visibility = _config.HideDecksInOverlay ? Visibility.Collapsed : Visibility.Visible;

			if(!_opponentCardsHidden)
				StackPanelOpponent.Visibility = _config.HideDecksInOverlay ? Visibility.Collapsed : Visibility.Visible;

			LblDrawChance1.Visibility = _config.HideDrawChances ? Visibility.Collapsed : Visibility.Visible;
			LblDrawChance2.Visibility = _config.HideDrawChances ? Visibility.Collapsed : Visibility.Visible;
			LblCardCount.Visibility = _config.HidePlayerCardCount ? Visibility.Collapsed : Visibility.Visible;
			LblDeckCount.Visibility = _config.HidePlayerCardCount ? Visibility.Collapsed : Visibility.Visible;

			LblOpponentDrawChance1.Visibility = _config.HideOpponentDrawChances
													? Visibility.Collapsed
													: Visibility.Visible;
			LblOpponentDrawChance2.Visibility = _config.HideOpponentDrawChances
													? Visibility.Collapsed
													: Visibility.Visible;
			LblOpponentCardCount.Visibility = _config.HideOpponentCardCount ? Visibility.Collapsed : Visibility.Visible;
			LblOpponentDeckCount.Visibility = _config.HideOpponentCardCount ? Visibility.Collapsed : Visibility.Visible;

			ListViewOpponent.Visibility = _config.HideOpponentCards ? Visibility.Collapsed : Visibility.Visible;
			ListViewPlayer.Visibility = _config.HidePlayerCards ? Visibility.Collapsed : Visibility.Visible;

			LblGrid.Visibility = Game.IsInMenu ? Visibility.Hidden : Visibility.Visible;

			DebugViewer.Visibility = _config.Debug ? Visibility.Visible : Visibility.Hidden;
			DebugViewer.Width = (Width * _config.TimerLeft / 100);

			SetCardCount(Game.PlayerHandCount,
						 Game.IsUsingPremade ? Game.PlayerDeck.Sum(c => c.Count) : 30 - Game.PlayerDrawn.Sum(c => c.Count));

			SetOpponentCardCount(Game.OpponentHandCount, Game.OpponentDeckCount);

			ReSizePosLists();
		}

		public bool PointInsideControl(Point pos, double actualWidth, double actualHeight)
		{
			if (pos.X > 0 && pos.X < actualWidth)
			{
				if (pos.Y > 0 && pos.Y < actualHeight)
				{
					return true;
				}
			}
			return false;
		}

		private void UpdateCardTooltip()
		{
			//todo: if distance to left or right of overlay < tooltip width -> switch side
			var pos = User32.GetMousePos();
			var relativePlayerDeckPos = StackPanelPlayer.PointFromScreen(new Point(pos.X, pos.Y));
			var relativeOpponentDeckPos = ListViewOpponent.PointFromScreen(new Point(pos.X, pos.Y));
			var relativeSecretsPos = StackPanelSecrets.PointFromScreen(new Point(pos.X, pos.Y));

			//player card tooltips
			if (PointInsideControl(relativePlayerDeckPos, ListViewPlayer.ActualWidth, ListViewPlayer.ActualHeight))
			{
				//card size = card list height / ammount of cards
				var cardSize = ListViewPlayer.ActualHeight / ListViewPlayer.Items.Count;
				var cardIndex = (int)(relativePlayerDeckPos.Y / cardSize);
				if (cardIndex < 0 || cardIndex >= ListViewPlayer.Items.Count)
					return;

				ToolTipCard.SetValue(DataContextProperty, ListViewPlayer.Items[cardIndex]);

				//offset is affected by scaling
				var topOffset = Canvas.GetTop(StackPanelPlayer) + cardIndex * cardSize * _config.OverlayPlayerScaling / 100;

				//prevent tooltip from going outside of the overlay
				if (topOffset + ToolTipCard.ActualHeight > Height)
					topOffset = Height - ToolTipCard.ActualHeight;

				SetTooltipPosition(topOffset, StackPanelPlayer);

				ToolTipCard.Visibility = Visibility.Visible;
			}
			//opponent card tooltips
			else if (PointInsideControl(relativeOpponentDeckPos, ListViewOpponent.ActualWidth, ListViewOpponent.ActualHeight))
			{
				//card size = card list height / ammount of cards
				var cardSize = ListViewOpponent.ActualHeight / ListViewOpponent.Items.Count;
				var cardIndex = (int)(relativeOpponentDeckPos.Y / cardSize);
				if (cardIndex < 0 || cardIndex >= ListViewOpponent.Items.Count)
					return;

				ToolTipCard.SetValue(DataContextProperty, ListViewOpponent.Items[cardIndex]);

				//offset is affected by scaling
				var topOffset = Canvas.GetTop(StackPanelOpponent) + cardIndex * cardSize * _config.OverlayOpponentScaling / 100;

				//prevent tooltip from going outside of the overlay
				if (topOffset + ToolTipCard.ActualHeight > Height)
					topOffset = Height - ToolTipCard.ActualHeight;

				SetTooltipPosition(topOffset, StackPanelOpponent);

				ToolTipCard.Visibility = Visibility.Visible;
			}
			else if (PointInsideControl(relativeSecretsPos, StackPanelSecrets.ActualWidth, StackPanelSecrets.ActualHeight))
			{
				//card size = card list height / ammount of cards
				var cardSize = StackPanelSecrets.ActualHeight / StackPanelSecrets.Children.Count;
				var cardIndex = (int)(relativeSecretsPos.Y / cardSize);
				if (cardIndex < 0 || cardIndex >= StackPanelSecrets.Children.Count)
					return;

				ToolTipCard.SetValue(DataContextProperty, StackPanelSecrets.Children[cardIndex].GetValue(DataContextProperty));

				//offset is affected by scaling
				var topOffset = Canvas.GetTop(StackPanelSecrets) + cardIndex * cardSize * _config.OverlayOpponentScaling / 100;

				//prevent tooltip from going outside of the overlay
				if (topOffset + ToolTipCard.ActualHeight > Height)
					topOffset = Height - ToolTipCard.ActualHeight;

				SetTooltipPosition(topOffset, StackPanelSecrets);

				ToolTipCard.Visibility = Visibility.Visible;
			}
			else
			{
				ToolTipCard.Visibility = Visibility.Hidden;
			}
		}

		private void SetTooltipPosition(double yOffset, StackPanel stackpanel)
		{
			Canvas.SetTop(ToolTipCard, yOffset);

			if (Canvas.GetLeft(stackpanel) < Width / 2)
			{
				Canvas.SetLeft(ToolTipCard, Canvas.GetLeft(stackpanel) + stackpanel.ActualWidth * _config.OverlayOpponentScaling / 100);
			}
			else
			{
				Canvas.SetLeft(ToolTipCard, Canvas.GetLeft(stackpanel) - ToolTipCard.Width);
			}
		}


		public void UpdatePosition()
		{
			//hide the overlay depenting on options
			ShowOverlay(!(
							 (_config.HideInBackground && !User32.IsForegroundWindow("Hearthstone"))
							 || (_config.HideInMenu && Game.IsInMenu)
							 || _config.HideOverlay));


			var hsRect = User32.GetHearthstoneRect(true);

			//hs window has height 0 if it just launched, screwing things up if the tracker is started before hs is. 
			//this prevents that from happening. 
			if (hsRect.Height == 0)
			{
				return;
			}

			SetRect(hsRect.Top, hsRect.Left, hsRect.Width, hsRect.Height);
			ReSizePosLists();

			if (_config.OverlayCardToolTips)
				UpdateCardTooltip();
		}


		internal void UpdateTurnTimer(TimerEventArgs timerEventArgs)
		{
			if (timerEventArgs.Running && (timerEventArgs.PlayerSeconds > 0 || timerEventArgs.OpponentSeconds > 0))
			{
				ShowTimers();

				LblTurnTime.Text = string.Format("{0:00}:{1:00}", (timerEventArgs.Seconds / 60) % 60,
												 timerEventArgs.Seconds % 60);
				LblPlayerTurnTime.Text = string.Format("{0:00}:{1:00}", (timerEventArgs.PlayerSeconds / 60) % 60,
													   timerEventArgs.PlayerSeconds % 60);
				LblOpponentTurnTime.Text = string.Format("{0:00}:{1:00}", (timerEventArgs.OpponentSeconds / 60) % 60,
														 timerEventArgs.OpponentSeconds % 60);

				if (_config.Debug)
				{
					LblDebugLog.Text += string.Format("Current turn: {0} {1} {2} \n",
													  timerEventArgs.CurrentTurn.ToString(),
													  timerEventArgs.PlayerSeconds.ToString(),
													  timerEventArgs.OpponentSeconds.ToString());
					DebugViewer.ScrollToBottom();
				}
			}
		}

		public void UpdateScaling()
		{
			_config.OverlayPlayerScaling += 0.00001;
			_config.OverlayOpponentScaling += 0.00001;
			StackPanelPlayer.RenderTransform = new ScaleTransform(_config.OverlayPlayerScaling / 100,
																  _config.OverlayPlayerScaling / 100);
			StackPanelOpponent.RenderTransform = new ScaleTransform(_config.OverlayOpponentScaling / 100,
																	_config.OverlayOpponentScaling / 100);
			StackPanelSecrets.RenderTransform = new ScaleTransform(_config.OverlayOpponentScaling / 100,
																	_config.OverlayOpponentScaling / 100);
		}

		public void HideTimers()
		{
			LblPlayerTurnTime.Visibility = LblOpponentTurnTime.Visibility = LblTurnTime.Visibility = Visibility.Hidden;
		}

		public void ShowTimers()
		{
			LblPlayerTurnTime.Visibility =
				LblOpponentTurnTime.Visibility =
				LblTurnTime.Visibility = _config.HideTimers ? Visibility.Hidden : Visibility.Visible;
		}

		public void SetOpponentTextLocation(bool top)
		{
			StackPanelOpponent.Children.Clear();
			if (top)
			{
				StackPanelOpponent.Children.Add(LblOpponentDrawChance2);
				StackPanelOpponent.Children.Add(LblOpponentDrawChance1);
				StackPanelOpponent.Children.Add(StackPanelOpponentCount);
				StackPanelOpponent.Children.Add(ListViewOpponent);
			}
			else
			{
				StackPanelOpponent.Children.Add(ListViewOpponent);
				StackPanelOpponent.Children.Add(LblOpponentDrawChance2);
				StackPanelOpponent.Children.Add(LblOpponentDrawChance1);
				StackPanelOpponent.Children.Add(StackPanelOpponentCount);
			}
		}

		public void SetPlayerTextLocation(bool top)
		{
			StackPanelPlayer.Children.Clear();
			if (top)
			{
				StackPanelPlayer.Children.Add(StackPanelPlayerDraw);
				StackPanelPlayer.Children.Add(StackPanelPlayerCount);
				StackPanelPlayer.Children.Add(ListViewPlayer);
			}
			else
			{
				StackPanelPlayer.Children.Add(ListViewPlayer);
				StackPanelPlayer.Children.Add(StackPanelPlayerDraw);
				StackPanelPlayer.Children.Add(StackPanelPlayerCount);
			}
		}

		public void ShowSecrets(string hsClass)
		{
			if (_config.HideSecrets) return;
			if (_lastSecretsClass != hsClass || _needToRefreshSecrets)
			{
				List<string> ids;
				switch (hsClass)
				{
					case "Hunter":
						ids = Game.SecretIdsHunter;
						break;
					case "Mage":
						ids = Game.SecretIdsMage;
						break;
					case "Paladin":
						ids = Game.SecretIdsPaladin;
						break;
					default:
						return;

				}
				StackPanelSecrets.Children.Clear();
				foreach (var id in ids)
				{
					var cardObj = new Controls.Card();
					cardObj.SetValue(DataContextProperty, Game.GetCardFromId(id));
					StackPanelSecrets.Children.Add(cardObj);
				}
				_lastSecretsClass = hsClass;
				_needToRefreshSecrets = false;
			}
			StackPanelSecrets.Visibility = Visibility.Visible;

		}

		public void HideSecrets()
		{
			StackPanelSecrets.Visibility = Visibility.Collapsed;
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			_mouseInput.Dispose();
		}
	}
}