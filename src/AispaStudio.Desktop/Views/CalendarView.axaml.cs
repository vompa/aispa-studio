using AispaStudio.Desktop.Controls;
using AispaStudio.Desktop.ViewModels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CalendarItem = AispaStudio.Desktop.ViewModels.CalendarItem;

namespace AispaStudio.Desktop.Views;

/// <summary>
/// Maus, Tastatur und Scrollen im Tageskalender. Was ein Klick, Ziehen oder eine Taste bedeutet, entscheidet das ViewModel;
/// hier werden nur Positionen umgerechnet, Tasten durchgereicht und Kopfzeile sowie Zeitleiste mit dem Inhalt mitgescrollt.
/// </summary>
public partial class CalendarView : UserControl
{
    /// <summary>Ab so vielen Pixeln Bewegung wird aus einem Klick ein Ziehen.</summary>
    private const double DragThreshold = 4;

    /// <summary>So breit ist der Streifen am oberen und unteren Rand einer Notiz oder Pause, an dem man die Höhe zieht.</summary>
    private const double EdgeGrip = 6;

    /// <summary>
    /// Spielraum, bevor ein Ziehen beim Verlassen der Kalenderfläche abbricht (Ä93): wer bewusst hinausfährt, bricht ab wie mit Esc; knapp am Rand nicht.
    /// </summary>
    private const double LeaveTolerance = 8;

    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };
    private CalendarItem? _pressedItem;
    private Point _pressedAt;
    private int _pressedClicks;
    private bool _pressedBlank;

    /// <summary>Gedrückt auf der Vorschau des neuen Termins: Bewegung zieht sie (Ä83).</summary>
    private bool _pressedPreview;

    /// <summary>Gedrückt am Rand einer Notiz oder Pause: Bewegung zieht den Rand (Ä88, Ä89).</summary>
    private (ResizeEdge Edge, CalendarNoteItem? Note, CalendarItem? Item, double EdgeY)? _pressedEdge;
    private IPointer? _dragPointer;
    private CalendarViewModel? _subscribed;
    private TopLevel? _topLevel;

    /// <summary>
    /// Wo die Maus beim Öffnen eines Termins stand (im Fenster). Das Panel macht den Kalender schmaler, unter der stehenden Maus
    /// läge dann eine freie Stelle; die Buchungsvorschau wartet, bis die Maus sich wirklich bewegt.
    /// </summary>
    private Point? _hoverPausedAt;

    public CalendarView()
    {
        InitializeComponent();
        BodyScroll.ScrollChanged += (_, _) =>
        {
            HeaderScroll.Offset = new Vector(BodyScroll.Offset.X, 0);
            GutterScroll.Offset = new Vector(0, BodyScroll.Offset.Y);
            ViewModel?.SetHorizontalView(BodyScroll.Offset.X - Host.Margin.Left, BodyScroll.Viewport.Width);
        };
        BodyScroll.SizeChanged += (_, _) => ReportViewport();
        DataContextChanged += (_, _) =>
        {
            ReportViewport();
            Subscribe();
        };

        // Tunnel, damit Pfeiltasten nicht vorher vom ScrollViewer verbraucht werden.
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnText, RoutingStrategies.Tunnel);

        // Ein Mausklick beendet den Tastaturmodus: Seitenleisten bewegen sich wieder (bei Kurzbefehlen erscheinen sie ohne Bewegung).
        AddHandler(PointerPressedEvent, (_, _) =>
        {
            if (ViewModel is { Presence.UsingKeyboard: true } vm)
            {
                vm.Presence.UsingKeyboard = false;
            }
        }, RoutingStrategies.Tunnel);

        // Strg + Mausrad zoomt, statt zu scrollen.
        BodyScroll.AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (ViewModel is { } vm && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                (e.Delta.Y > 0 ? vm.ZoomInCommand : vm.ZoomOutCommand).Execute(null);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        Host.PointerPressed += OnHostPressed;
        Host.PointerMoved += OnHostMoved;
        Host.PointerReleased += OnHostReleased;
        Host.PointerExited += (_, _) =>
        {
            ViewModel?.ClearHover();
            if (ViewModel?.IsDragging != true)
            {
                ShowPointer(StandardCursorType.Arrow);
            }
        };
        Host.PointerCaptureLost += (_, _) =>
        {
            if (ViewModel?.IsDragging == true)
            {
                ViewModel.CancelDrag();
                ShowPointer(StandardCursorType.Arrow);
            }

            ClearPressed();
        };
        _clock.Tick += (_, _) => ViewModel?.Tick();
    }

    /// <summary>Form des Mauszeigers im Kalender: Hand über verschiebbaren Terminen, Kreuz beim Ziehen, sonst Pfeil.</summary>
    public StandardCursorType PointerShape { get; private set; } = StandardCursorType.Arrow;

    private CalendarViewModel? ViewModel => DataContext as CalendarViewModel;

    private void ShowPointer(StandardCursorType type)
    {
        if (type != PointerShape)
        {
            PointerShape = type;
            Cursor = new Cursor(type);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _clock.Start();
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnKeyWithoutFocus, RoutingStrategies.Tunnel);
        Dispatcher.UIThread.Post(() => Focus());
    }

    private void Subscribe()
    {
        if (_subscribed is { } old)
        {
            old.SearchRequested -= FocusSearch;
            old.DatePickerRequested -= OpenDatePicker;
            old.RequestOpened -= FocusRequest;
            old.PanelOpened -= FocusPanel;
            old.PanelFocusRequested -= FocusInPanel;
            old.ColumnRevealRequested -= RevealColumn;
            old.MarkMovedByKey -= FocusCard;
            old.PropertyChanged -= OnViewModelChanged;
        }

        _subscribed = ViewModel;
        if (_subscribed is { } vm)
        {
            vm.SearchRequested += FocusSearch;
            vm.DatePickerRequested += OpenDatePicker;
            vm.RequestOpened += FocusRequest;
            vm.PanelOpened += FocusPanel;
            vm.PanelFocusRequested += FocusInPanel;
            vm.ContactActions ??= new WindowContactActions(this);
            vm.Files ??= new StorageFilePicker(this);
            vm.FileSaver ??= new StorageFileSaver(this);
            vm.ColumnRevealRequested += RevealColumn;
            vm.MarkMovedByKey += FocusCard;
            vm.PropertyChanged += OnViewModelChanged;
        }
    }

    /// <summary>
    /// Pfeiltasten haben einen anderen Termin markiert: seine Karte bekommt den Fokus, damit ein Screenreader Zeit, Gast, Behandlung und Status ansagt
    /// (R01 Slice 18, W50). Die Tasten kommen weiter an, weil die Ansicht sie im Tunnel vor der Karte behandelt. Nach dem Scrollen, damit die Karte im Bild ist.
    /// Ohne Navigationsart, sonst öffnete <see cref="ToolTipBehavior"/> bei jedem Pfeil die Hover-Karte über den Nachbarterminen.
    /// </summary>
    private void FocusCard(Guid appointmentId) =>
        Dispatcher.UIThread.Post(() =>
            Host.GetVisualDescendants().OfType<AppointmentCard>().FirstOrDefault(c => c.DataContext is CalendarItem { Appointment.Id: var id } && id == appointmentId)
                ?.Focus(), DispatcherPriority.Background);

    private void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void OpenDatePicker() => DateButton.Flyout?.ShowAt(DateButton);

    /// <summary>
    /// Ein gewählter Tag und „Heute“ schließen das Datums-Flyout; vorher blieb es offen, bis man daneben klickte (Auftraggeber, 23.10.2026).
    /// Das Schließen kommt nach dem Ereignis: der Calendar meldet die Auswahl, bevor die Bindung den Tag ins ViewModel schreibt, und
    /// ein sofort geschlossenes Flyout nimmt dem Inhalt den DataContext (der Tag ginge verloren).
    /// </summary>
    private void OnDatePicked(object? sender, SelectionChangedEventArgs e)
    {
        // Beim Öffnen setzt die Bindung den aktuellen Tag; das ist keine Auswahl und darf das Flyout nicht schließen.
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is DateTime picked && ViewModel is { } vm && DateOnly.FromDateTime(picked) != vm.Date)
        {
            Dispatcher.UIThread.Post(() => DateButton.Flyout?.Hide());
        }
    }

    private void OnPickToday(object? sender, RoutedEventArgs e)
    {
        ViewModel?.TodayCommand.Execute(null);
        DateButton.Flyout?.Hide();
    }

    /// <summary>
    /// Eine Leiste zum Eingeben ist aufgegangen: der Fokus wandert ins erste Feld (bei „Freie Zeit suchen“ in die Behandlung), damit man gleich tippen kann.
    /// Beim reinen Ansehen eines Termins bleibt er im Kalender, damit die Pfeiltasten weiter wirken. Beim Schließen holt der Kalender ihn zurück.
    /// </summary>
    private void FocusPanel() =>
        Dispatcher.UIThread.Post(() =>
        {
            var fields = SidePanel.GetVisualDescendants().OfType<Control>().Where(c => c is TextBox or ComboBox && c.IsEffectivelyVisible && c.IsEnabled && c.Focusable).ToList();
            (fields.FirstOrDefault(c => c.Name == "FreeTimeTreatment") ?? fields.FirstOrDefault())?.Focus();
        }, DispatcherPriority.Background);

    /// <summary>
    /// Eine Seite bittet um den Fokus auf ein Element mit diesem Namen: das Notizfeld, „+ Notiz“, den Stift an der Notiz. Gibt es das Element nicht (mehr),
    /// holt der Kalender den Fokus, damit die Tasten weiter wirken. Die Karte um das Element kommt ganz ins Bild: der Fokus allein scrollt nur bis zum Element,
    /// beim Sprung zur Anamnese lägen die Wege darunter sonst außerhalb (T-022).
    /// </summary>
    private void FocusInPanel(string name) =>
        Dispatcher.UIThread.Post(() =>
        {
            var target = SidePanel.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.IsEffectivelyEnabled && c.Focusable);
            if (target is null)
            {
                Focus();
                return;
            }

            target.Focus(NavigationMethod.Tab);
            target.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("panelcard"))?.BringIntoView();
        }, DispatcherPriority.Background);

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalendarViewModel.MarkedId))
        {
            Dispatcher.UIThread.Post(ScrollToMarked, DispatcherPriority.Background);
        }
        else if (e.PropertyName == nameof(CalendarViewModel.PanelWidth) && ViewModel?.Selected is { IsEditingCustomer: true })
        {
            // Die Kundendaten gehen im Panel auf: der Fokus liegt gleich im ersten Feld.
            Dispatcher.UIThread.Post(() => PanelCustomerForm.FocusFirstField(), DispatcherPriority.Background);
        }
    }

    /// <summary>Knopf „Zurück“ am Rand (Ä154): um die Breite des sichtbaren Ausschnitts zurück.</summary>
    private void OnEdgePreviousClick(object? sender, RoutedEventArgs e) => ScrollByViewport(-1);

    private void OnKeyHelpScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        ViewModel?.ToggleKeyHelpCommand.Execute(null);
        e.Handled = true;
    }

    /// <summary>Knopf „Weiter“ am Rand (Ä154): um die Breite des sichtbaren Ausschnitts weiter.</summary>
    private void OnEdgeNextClick(object? sender, RoutedEventArgs e) => ScrollByViewport(1);

    private void ScrollByViewport(int direction)
    {
        var max = Math.Max(0, BodyScroll.Extent.Width - BodyScroll.Viewport.Width);
        var x = Math.Clamp(BodyScroll.Offset.X + (direction * BodyScroll.Viewport.Width), 0, max);
        BodyScroll.Offset = new Vector(x, BodyScroll.Offset.Y);
    }

    /// <summary>Markierter Termin außerhalb des sichtbaren Ausschnitts: so weit scrollen, dass er ganz zu sehen ist.</summary>
    private void ScrollToMarked()
    {
        if (ViewModel is not { MarkedBounds: { } bounds } vm)
        {
            return;
        }

        var view = BodyScroll.Viewport;
        var offset = BodyScroll.Offset;
        var x = offset.X;
        var y = offset.Y;
        var left = vm.MarkedLeft + 8;
        if (left < x || left + vm.Scale.ColumnWidth > x + view.Width)
        {
            x = Math.Max(0, left - 8);
        }

        var top = bounds.Top + 10;
        if (top < y || top + bounds.Height > y + view.Height)
        {
            y = Math.Max(0, top - 40);
        }

        BodyScroll.Offset = new Vector(x, y);
    }

    /// <summary>
    /// Tasten: im Suchfeld nur Enter und Esc, in anderen Eingabefeldern und im Seitenpanel nichts außer Esc;
    /// sonst entscheidet das ViewModel.
    /// </summary>
    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        // Esc beim Ziehen: nur abbrechen. Die Maus freigeben, damit das Loslassen danach nichts mehr bewirkt (kein Öffnen, kein Verschieben).
        if (e.Key == Key.Escape && vm.IsDragging)
        {
            AbortDrag();
            e.Handled = true;
            return;
        }

        // Strg+Tab und Strg+Umschalt+Tab wechseln den Reiter der Seitenleiste, auch mitten im Tippen (ohne Bewegung, es ist ein Kurzbefehl).
        if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control) && vm.HasPanel)
        {
            vm.Presence.UsingKeyboard = true;
            e.Handled = vm.CyclePanelTab(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
            return;
        }

        // Alt+1 bis Alt+6 wechselt im offenen Eingabefenster die Art, auch mitten im Tippen.
        if (e.KeyModifiers == KeyModifiers.Alt && vm.NewEntry is not null && CalendarViewModel.NumberOf(e.Key) is { } number && vm.SelectNewEntry(number))
        {
            e.Handled = true;
            return;
        }

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focused == SearchBox)
        {
            if (e.Key == Key.Enter)
            {
                vm.OpenSearchHit(null);
                Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Esc in Stufen (Design-Brief 5.16): zuerst die Trefferliste zu, dann das Feld leeren, dann das Feld verlassen.
                if (!vm.DismissSearchList())
                {
                    if (string.IsNullOrEmpty(vm.SearchText))
                    {
                        Focus();
                    }
                    else
                    {
                        vm.ClearSearch();
                    }
                }

                e.Handled = true;
            }

            return;
        }

        if (focused is TextBox { Name: "RequestBox" } && vm.Request is { } request && HandleRequestBoxKey(vm, request, e))
        {
            e.Handled = true;
            return;
        }

        // Strg+W schließt die gewählte Seite, Strg+1 bis 9 wählen einen Reiter; wie Strg+Tab auch mitten im Tippen.
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            var previousMode = vm.Presence.UsingKeyboard;
            vm.Presence.UsingKeyboard = true;
            if (vm.HandleTabShortcut(e.Key))
            {
                e.Handled = true;
                return;
            }

            vm.Presence.UsingKeyboard = previousMode;
        }

        var inPanel = focused is not null && SidePanel.IsVisualAncestorOf(focused);

        // Esc in einem Eingabefeld (Design-Brief 5.16): hat das Feld einen geänderten Wert, stellt es ihn zuerst selbst wieder her und verbraucht
        // das Esc; erst das zweite Esc (nichts mehr zurückzustellen) schließt die Seitenleiste bzw. bricht das Ändern ab.
        var pendingFieldChange = e.Key == Key.Escape && FieldBehaviors.HasPendingChange(focused);

        // Esc in den Zahlenfeldern von Vor- und Nachbereitung bricht das Ändern ab, auch wenn das Feld den Fokus hat.
        if (e.Key == Key.Escape && inPanel && !pendingFieldChange && vm.Selected is { IsEditingTimes: true } && vm.HandleKey(Key.Escape, KeyModifiers.None))
        {
            Focus();
            e.Handled = true;
            return;
        }

        var freeTimeKey = (vm.FreeTime is { HasSlots: true } || vm.Request is { HasProposals: true })
            && e.Key is Key.Up or Key.Down or Key.Enter && e.KeyModifiers == KeyModifiers.None;
        var escapeClosesPanel = e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && inPanel && !pendingFieldChange;
        if ((focused is TextBox && !escapeClosesPanel) || (inPanel && e.Key != Key.Escape && !freeTimeKey))
        {
            return;
        }

        // Vor dem Tastendruck merken, dass die Tastatur im Spiel ist: öffnet oder schließt er eine Seitenleiste, soll sie ohne Bewegung erscheinen.
        var wasUsingKeyboard = vm.Presence.UsingKeyboard;
        vm.Presence.UsingKeyboard = true;
        if (vm.HandleKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            if (inPanel && !vm.HasPanel)
            {
                Focus();
            }
        }
        else
        {
            vm.Presence.UsingKeyboard = wasUsingKeyboard;
        }
    }

    /// <summary>
    /// Verschwindet das Steuerelement mit dem Fokus (Schließen-Knopf des Panels, Meldung), hat im Fenster niemand mehr den Fokus,
    /// und die Taste käme nie im Kalender an. Dann holt der Kalender den Fokus zurück und behandelt die Taste selbst.
    /// </summary>
    private void OnKeyWithoutFocus(object? sender, KeyEventArgs e)
    {
        if (_topLevel?.FocusManager?.GetFocusedElement() is null && !e.Handled)
        {
            Focus();
            OnKey(sender, e);
        }
    }

    private void OnText(object? sender, TextInputEventArgs e)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is not TextBox && ViewModel?.HandleText(e.Text) == true)
        {
            e.Handled = true;
        }
    }

    /// <summary>Im Eingabefeld der KI-Anfrage: Enter sucht bzw. bucht, ↑ ↓ wechseln den Vorschlag, Strg+1 bis 3 bucht, Esc schließt.</summary>
    private bool HandleRequestBoxKey(CalendarViewModel vm, CalendarRequestViewModel request, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when e.KeyModifiers == KeyModifiers.None:
                _ = request.SubmitAsync();
                return true;
            case Key.Up when e.KeyModifiers == KeyModifiers.None:
                request.Move(-1);
                return true;
            case Key.Down when e.KeyModifiers == KeyModifiers.None:
                request.Move(1);
                return true;
            case Key.D1 or Key.D2 or Key.D3 when e.KeyModifiers == KeyModifiers.Control && request.HasProposals:
                _ = request.BookAsync(e.Key - Key.D0);
                return true;
            case Key.Escape:
                vm.HandleKey(Key.Escape, KeyModifiers.None);
                Focus();
                return true;
            default:
                return false;
        }
    }

    private void FocusRequest() =>
        Dispatcher.UIThread.Post(() => this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "RequestBox")?.Focus(),
            DispatcherPriority.Background);

    /// <summary>Antwort auf eine Rückfrage („Zwei Personen (Paarraum)“): der Wunsch mit der Antwort geht zurück an den Server.</summary>
    private void OnAnswerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: RequestAnswer answer } && ViewModel?.Request is { } request)
        {
            _ = request.AnswerCommand.ExecuteAsync(answer);
            FocusRequest();
        }
    }

    /// <summary>Karte oder Eintrag aus „Alle Behandlungen“: die Behandlung gehört jetzt zum Wunsch.</summary>
    private void OnTreatmentCardClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: TreatmentCard card } && ViewModel?.Request is { } request)
        {
            _ = request.ChooseTreatmentCommand.ExecuteAsync(card);
            FocusRequest();
        }
    }

    private void OnRemoveChip(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: WishChip chip } && ViewModel?.Request is { } request)
        {
            _ = request.RemoveAsync(chip);
            FocusRequest();
        }
    }

    private void OnProposalTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: ProposalRow row } && ViewModel?.Request is { } request)
        {
            request.Choose(row);
        }
    }


    private void OnSearchHitClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: CalendarSearchHit hit } && ViewModel is { } vm)
        {
            vm.OpenSearchHit(hit);
            Focus();
        }
    }

    /// <summary>
    /// Rechtsklick-Menü aus den Einträgen des ViewModels (Design-Brief 5.21): häufige Aktionen oben, riskante unten nach einem Trenner in danger, Tastenkürzel als Nebeninhalt rechts,
    /// keine Untermenüs. Esc schließt, der Fokus kehrt dorthin zurück, wo er war.
    /// </summary>
    private void ShowMenu(IReadOnlyList<CalendarAction> actions)
    {
        if (actions.Count == 0)
        {
            return;
        }

        var items = new List<Control>();
        var reserveIcon = actions.Any(a => a.Icon is not null);
        foreach (var action in actions)
        {
            if (action.IsDanger)
            {
                items.Add(new Separator());
            }

            items.Add(MenuItemFor(action, reserveIcon));
        }

        var previous = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var menu = new ContextMenu { ItemsSource = items };
        menu.Closed += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (previous is Visual { IsEffectivelyVisible: true } visual && TopLevel.GetTopLevel(visual) is not null)
            {
                previous.Focus();
            }
            else
            {
                Focus();
            }
        }, DispatcherPriority.Background);
        menu.Open(Host);
    }

    /// <summary>
    /// Ein Eintrag des Menüs. Die wichtigsten tragen ihr Symbol (16, Brief 5.21); hat das Menü Symbole, halten die übrigen die Stelle frei, damit alle Texte in einer Linie beginnen.
    /// </summary>
    private static MenuItem MenuItemFor(CalendarAction action, bool reserveIcon)
    {
        var item = new MenuItem { Header = action.Header };
        if (action.Icon is { } icon)
        {
            item.Icon = new Icon { Data = icon, Width = 16, Height = 16 };
        }
        else if (reserveIcon)
        {
            item.Icon = new Border { Width = 16, Height = 16 };
        }

        AutomationProperties.SetName(item, action.Header);
        if (action.Gesture is { } gesture)
        {
            AutomationProperties.SetAcceleratorKey(item, gesture);
            // Das Tastenkürzel steht rechts als Nebeninhalt (Stufe „Klein“, fg-2); die Taste selbst gehört dem Kalender, deshalb kein InputGesture.
            var key = new TextBlock { Text = gesture, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            key.Classes.Add("menugesture");
            Grid.SetColumn(key, 1);
            item.Header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = action.Header }, key } };
        }

        item.Classes.Set("danger", action.IsDanger);
        item.Click += (_, _) => action.Run?.Invoke();
        return item;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _clock.Stop();
        _topLevel?.RemoveHandler(KeyDownEvent, OnKeyWithoutFocus);
        _topLevel = null;
        // Die Trefferliste ist ein eigenes Fenster und bliebe sonst über der nächsten Ansicht stehen.
        ViewModel?.ClearSearch();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnHostPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        Focus();
        var point = e.GetCurrentPoint(Host);

        // Die Vorschau des neuen Termins liegt über allem in der Spalte: ein Druck darauf greift sie (ohne Bewegung bleibt es ein Klick an diese Stelle).
        if (point.Properties.IsLeftButtonPressed && vm.IsOnPreview(point.Position.X, point.Position.Y))
        {
            ClearPressed();
            _pressedAt = point.Position;
            _pressedClicks = e.ClickCount;
            _pressedBlank = true;
            _pressedPreview = true;
            e.Pointer.Capture(Host);
            _dragPointer = e.Pointer;
            e.Handled = true;
            return;
        }

        if (NoteUnder(e.Source) is { } note)
        {
            // Zettel: Klick öffnet die Bearbeitung (wie bei Terminen), Rechtsklick bietet Details, Abhaken und Löschen; am Rand zieht man die Höhe.
            if (point.Properties.IsRightButtonPressed)
            {
                ShowMenu(vm.ActionsFor(note.Note));
            }
            else if (point.Properties.IsLeftButtonPressed && vm.CanResize(note) && NoteControlUnder(e.Source) is { } sticky && EdgeOf(sticky, e) is { } noteEdge)
            {
                PressEdge(e, point.Position, (noteEdge, note, null, noteEdge == ResizeEdge.Top ? note.Top : note.Top + note.Height));
            }
            else if (point.Properties.IsLeftButtonPressed)
            {
                vm.OpenNote(note.Note);
            }

            e.Handled = true;
            return;
        }

        if (point.Properties.IsRightButtonPressed)
        {
            if (ItemUnder(e.Source) is null && StripeUnder(e.Source) is { } stripe && vm.ActionsForStripe(stripe) is { Count: > 0 } stripeActions)
            {
                ShowMenu(stripeActions);
                e.Handled = true;
                return;
            }

            var under = ItemUnder(e.Source);
            if (under is not null)
            {
                vm.Mark(under);
            }

            ShowMenu(under is not null ? vm.ActionsFor(under) : vm.ActionsAt(point.Position.X, point.Position.Y));
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Raumpflege-Streifen: ein Klick hakt ab (statt eine Buchung an dieser Stelle zu beginnen).
        if (vm.CanCheckCare && ItemUnder(e.Source) is null && StripeUnder(e.Source) is { HasCare: true } careStripe)
        {
            _ = vm.ToggleCareAsync(careStripe);
            e.Handled = true;
            return;
        }

        if (ItemUnder(e.Source) is { } edgeItem && vm.CanResize(edgeItem) && ItemControlUnder(e.Source) is { } bar && EdgeOf(bar, e) is { } itemEdge)
        {
            PressEdge(e, point.Position, (itemEdge, null, edgeItem, itemEdge == ResizeEdge.Top ? edgeItem.Top : edgeItem.Top + edgeItem.Height));
            e.Handled = true;
            return;
        }

        ClearPressed();
        _pressedAt = point.Position;
        _pressedClicks = e.ClickCount;
        _pressedItem = ItemUnder(e.Source);
        _pressedBlank = _pressedItem is null;
        if (_pressedItem is not null)
        {
            // Eine offene Hover-Karte würde dem Klick im Weg stehen.
            if (ItemControlUnder(e.Source) is { } card)
            {
                ToolTip.SetIsOpen(card, false);
            }

            e.Pointer.Capture(Host);
            _dragPointer = e.Pointer;
        }

        e.Handled = true;
    }

    private void OnHostMoved(object? sender, PointerEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        var position = e.GetPosition(Host);
        if (!vm.IsDragging && Distance(position, _pressedAt) > DragThreshold)
        {
            BeginPressedDrag(vm);
        }

        if (vm.IsDragging)
        {
            // Bewusst aus der Kalenderfläche hinaus: abbrechen wie mit Esc, beim Loslassen wird nichts verschoben (Ä93).
            if (IsOutsideCalendar(e))
            {
                AbortDrag();
                return;
            }

            vm.DragTo(position.X, position.Y);
            return;
        }

        if (_hoverPausedAt is { } paused)
        {
            if (Distance(e.GetPosition(TopLevel.GetTopLevel(this)), paused) <= DragThreshold)
            {
                vm.ClearHover();
                return;
            }

            _hoverPausedAt = null;
            vm.HoverCardsPaused = false;
        }

        // Neu treffen statt e.Source: nach einem Neuaufbau der Termine meldet die Bewegung sonst noch das entfernte Element,
        // und über dem Termin erschiene die Buchungsvorschau.
        var under = Host.InputHitTest(position);
        var itemUnder = ItemUnder(under);
        // Greifzeiger nur dort, wo man ziehen darf, am Rand einer Notiz oder Pause der Zeiger für die Höhe; sonst bleibt es der normale Zeiger.
        ShowPointer(PointerAt(vm, under, position, e));
        if (itemUnder is null && NoteUnder(under) is null)
        {
            vm.Hover(position.X, position.Y);
        }
        else
        {
            vm.ClearHover();
        }
    }

    private void OnToastEntered(object? sender, PointerEventArgs e) => ViewModel?.HoldToast(true);

    private void OnToastExited(object? sender, PointerEventArgs e) => ViewModel?.HoldToast(false);

    private async void OnHostReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        var position = e.GetPosition(Host);
        var item = _pressedItem;
        var blank = _pressedBlank;
        var edge = _pressedEdge;
        ClearPressed();
        ShowPointer(StandardCursorType.Arrow);

        // Ablegen beginnt vor dem Freigeben der Maus: das Freigeben löst PointerCaptureLost aus, und das bricht ein laufendes Ziehen ab.
        var drop = vm.IsDragging ? vm.DropAsync() : null;
        e.Pointer.Capture(null);

        if (drop is not null)
        {
            await drop.ConfigureAwait(true);
        }
        else if (edge is { Note: { } note })
        {
            // Am Rand gedrückt, aber nicht gezogen: wie ein Klick auf den Zettel.
            vm.OpenNote(note.Note);
        }
        else if (edge is { Item: { } bar })
        {
            vm.Open(bar);
        }
        else if (item is not null)
        {
            _hoverPausedAt = e.GetPosition(TopLevel.GetTopLevel(this));
            vm.HoverCardsPaused = true;
            vm.ClearHover();
            vm.Open(item);
        }
        else if (blank && Distance(position, _pressedAt) <= DragThreshold)
        {
            // Doppelklick in einer Spalte ohne Dienst bucht ohne die Rückfrage „Trotzdem buchen“ (Auftraggeber, 23.10.2026).
            vm.StartBookingAt(position.X, position.Y, offDutyConfirmed: _pressedClicks >= 2);
        }
    }

    private void ClearPressed()
    {
        _pressedItem = null;
        _pressedBlank = false;
        _pressedPreview = false;
        _pressedEdge = null;
    }

    /// <summary>Esc oder Mauszeiger außerhalb der Kalenderfläche: abbrechen und die Maus freigeben, damit das Loslassen danach nichts mehr bewirkt.</summary>
    private void AbortDrag()
    {
        ClearPressed();
        ViewModel?.CancelDrag();
        _dragPointer?.Capture(null);
        ShowPointer(StandardCursorType.Arrow);
    }

    /// <summary>Druck am Rand einer Notiz oder Pause: erst eine Bewegung macht daraus ein Ziehen, sonst bleibt es ein Klick.</summary>
    private void PressEdge(PointerPressedEventArgs e, Point at, (ResizeEdge Edge, CalendarNoteItem? Note, CalendarItem? Item, double EdgeY) edge)
    {
        ClearPressed();
        _pressedAt = at;
        _pressedClicks = e.ClickCount;
        _pressedEdge = edge;
        e.Pointer.Capture(Host);
        _dragPointer = e.Pointer;
    }

    /// <summary>Die Maus hat sich nach dem Drücken weit genug bewegt: je nachdem, was gedrückt wurde, beginnt das passende Ziehen.</summary>
    private void BeginPressedDrag(CalendarViewModel vm)
    {
        if (_pressedEdge is { } edge)
        {
            var grab = _pressedAt.Y - edge.EdgeY;
            if (edge.Note is { } note)
            {
                vm.BeginResize(note, edge.Edge, grab);
            }
            else if (edge.Item is { } bar)
            {
                vm.BeginResize(bar, edge.Edge, grab);
            }

            ShowPointer(StandardCursorType.SizeNorthSouth);
        }
        else if (_pressedPreview && vm.Ghost is { } preview)
        {
            vm.BeginPreviewDrag(_pressedAt.Y - preview.Top);
            ShowPointer(StandardCursorType.SizeAll);
        }
        else if (_pressedItem is { } item && vm.CanMove(item))
        {
            vm.BeginDrag(item, _pressedAt.Y - item.Top);
            ShowPointer(StandardCursorType.SizeAll);
        }
    }

    /// <summary>Der Mauszeiger steht klar außerhalb des sichtbaren Kalenders (Kopfzeile, Zeitleiste, Seitenleiste oder außerhalb des Fensters).</summary>
    private bool IsOutsideCalendar(PointerEventArgs e)
    {
        var at = e.GetPosition(BodyScroll);
        var size = BodyScroll.Bounds.Size;
        return at.X < -LeaveTolerance || at.Y < -LeaveTolerance || at.X > size.Width + LeaveTolerance || at.Y > size.Height + LeaveTolerance;
    }

    /// <summary>Zeiger über der Stelle: Höhe ziehen am Rand einer Notiz oder Pause, Hand über der Vorschau und verschiebbaren Terminen, sonst Pfeil.</summary>
    private static StandardCursorType PointerAt(CalendarViewModel vm, IInputElement? under, Point position, PointerEventArgs e)
    {
        if (vm.IsOnPreview(position.X, position.Y))
        {
            return StandardCursorType.Hand;
        }

        if (NoteUnder(under) is { } note)
        {
            return vm.CanResize(note) && NoteControlUnder(under) is { } sticky && EdgeOf(sticky, e) is not null ? StandardCursorType.SizeNorthSouth : StandardCursorType.Arrow;
        }

        if (ItemUnder(under) is not { } item)
        {
            return StandardCursorType.Arrow;
        }

        if (vm.CanResize(item) && ItemControlUnder(under) is { } bar && EdgeOf(bar, e) is not null)
        {
            return StandardCursorType.SizeNorthSouth;
        }

        return vm.CanMove(item) ? StandardCursorType.Hand : StandardCursorType.Arrow;
    }

    /// <summary>Oberer oder unterer Rand des Balkens unter dem Mauszeiger; in der Mitte keiner.</summary>
    private static ResizeEdge? EdgeOf(Control bar, PointerEventArgs e)
    {
        var y = e.GetPosition(bar).Y;
        return y <= EdgeGrip ? ResizeEdge.Top : y >= bar.Bounds.Height - EdgeGrip ? ResizeEdge.Bottom : null;
    }

    /// <summary>Platz für die Spalten: sichtbare Fläche ohne die Ränder um die Spalten (8 + 24 waagrecht, 10 + 24 senkrecht).</summary>
    private void ReportViewport()
    {
        var size = BodyScroll.Bounds.Size;
        if (size.Width > 0 && size.Height > 0)
        {
            ViewModel?.SetViewport(size.Width - 32, size.Height - 34);
        }
    }

    /// <summary>
    /// „Falsch melden“ in der Liste der Kalender-Abos: der Befehl öffnet die Rückfrage; das Fenster geht erst danach zu, weil der Knopf mit dem Schließen seinen DataContext verliert.
    /// </summary>
    private void OnSubscriptionAction(object? sender, RoutedEventArgs e) => Dispatcher.UIThread.Post(() => HeadSubscriptions.Flyout?.Hide());

    /// <summary>Klick auf eine Zahl im Tagesüberblick: der Befehl läuft, das Fenster geht zu, damit der Kalender zu sehen ist.</summary>
    private void OnOverviewAction(object? sender, RoutedEventArgs e) => OverviewChip.Flyout?.Hide();

    /// <summary>
    /// Ein Eintrag der Klappliste „⋯ n“ wurde gewählt: die Liste schließt, der Reiter wechselt über den Befehl des Eintrags. Das Schließen kommt erst danach,
    /// weil der Eintrag mit dem Schließen seinen DataContext und damit den Parameter des Befehls verliert.
    /// </summary>
    private void OnPanelTabPicked(object? sender, RoutedEventArgs e) => Dispatcher.UIThread.Post(() => PanelMore.Flyout?.Hide());

    /// <summary>Klick auf die Initialen im Terminkopf: Kundenübersicht neu laden, das Flyout öffnet der Knopf selbst.</summary>
    private void OnGuestOverviewClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: AppointmentViewModel appointment })
        {
            appointment.ReloadOverview();
        }
    }

    /// <summary>Klick (Enter, Leertaste) auf den Raum im Termin-Panel: die Raum-Infos öffnen links neben der Seitenleiste, damit sie das Panel nicht verdecken.</summary>
    private void OnRoomInfoClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control source && FlyoutBase.GetAttachedFlyout(source) is { } flyout)
        {
            // Am Ziel (Seitenleiste) hängt der DataContext des Kalenders, nicht der des Termins: die Karte bekommt ihren eigenen.
            if (flyout is Flyout { Content: Control card })
            {
                card.DataContext = source.DataContext;
            }

            flyout.ShowAt(SidePanel);
        }
    }

    /// <summary>Klick auf den Namen im Terminkopf: wie bei den Initialen die Kundenübersicht zeigen.</summary>
    private void OnGuestNamePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: AppointmentViewModel { CanShowOverview: true } appointment } name
            && e.GetCurrentPoint(name).Properties.IsLeftButtonPressed)
        {
            appointment.ReloadOverview();
            FlyoutBase.ShowAttachedFlyout(name);
            e.Handled = true;
        }
    }

    /// <summary>Der Kopf des Abschnitts „Kunde“ nimmt Enter selbst an; deshalb hört der Rahmen im Tunnel mit, damit Strg+Enter vor dem Knopf ankommt.</summary>
    private void OnCustomerSectionLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control section)
        {
            section.RemoveHandler(KeyDownEvent, OnCustomerSectionKey);
            section.AddHandler(KeyDownEvent, OnCustomerSectionKey, RoutingStrategies.Tunnel);
        }
    }

    /// <summary>Strg+Enter im Abschnitt „Kunde“ des Termin-Panels öffnet die Kundenkarte (das Kürzel steht im Hinweis am Link).</summary>
    private void OnCustomerSectionKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control && sender is Control { DataContext: CustomerSectionViewModel { CanOpenCard: true } section })
        {
            section.OpenCardCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>Öffnet Links und kopiert Text für die Kontaktzeilen im Termin-Panel über das Fenster, in dem der Kalender steht.</summary>
    internal sealed class WindowContactActions(Visual owner) : Services.IContactActions
    {
        public async Task LaunchAsync(Uri uri)
        {
            if (TopLevel.GetTopLevel(owner)?.Launcher is { } launcher)
            {
                await launcher.LaunchUriAsync(uri).ConfigureAwait(true);
            }
        }

        public async Task CopyAsync(string text)
        {
            if (TopLevel.GetTopLevel(owner)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text).ConfigureAwait(true);
            }
        }
    }

    /// <summary>
    /// Ein Eintrag unter „Als Nächstes“: der Termin wird im Kalender markiert und sein Panel öffnet rechts; das Fenster geht erst danach zu,
    /// weil der Eintrag mit dem Schließen seinen DataContext verliert.
    /// </summary>
    private void OnUpcomingClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: UpcomingItem upcoming } && ViewModel is { } vm)
        {
            vm.OpenAppointment(upcoming.Appointment);
        }

        Dispatcher.UIThread.Post(() => OverviewChip.Flyout?.Hide());
    }

    /// <summary>Pfeile, Pos1 und Ende in der Liste „Als Nächstes“ bewegen den Fokus von Eintrag zu Eintrag (ohne Umlauf); Enter löst der Knopf selbst aus.</summary>
    private void OnUpcomingKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Down or Key.Up or Key.Home or Key.End) || sender is not ItemsControl list)
        {
            return;
        }

        var entries = list.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled).ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var current = entries.FindIndex(b => b.IsFocused);
        var next = e.Key switch
        {
            Key.Home => 0,
            Key.End => entries.Count - 1,
            Key.Down => Math.Min(current + 1, entries.Count - 1),
            _ => Math.Max(current - 1, 0),
        };
        entries[next].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    /// <summary>Termin, auf den der Mauszeiger zeigt (oder null für eine freie Stelle).</summary>
    private static CalendarItem? ItemUnder(object? source) => ItemControlUnder(source)?.DataContext as CalendarItem;

    private static Control? ItemControlUnder(object? source) =>
        (source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(c => c.Classes.Contains("appt"));

    /// <summary>
    /// Linksklick auf den Kopf einer Person oder eines Raums: Termine über sieben Tage, auf den Kopf eines Tages: dieser Tag in der normalen Ansicht;
    /// Rechtsklick: Ersatz suchen, krank melden.
    /// </summary>
    private void OnColumnHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: CalendarColumn column } control || ViewModel is not { } vm)
        {
            return;
        }

        var point = e.GetCurrentPoint(control).Properties;
        if (point.IsRightButtonPressed)
        {
            ShowMenu(vm.ActionsForColumn(column));
            e.Handled = true;
        }
        else if (point.IsLeftButtonPressed && column.CanFilter)
        {
            Focus();
            vm.FilterBy(column);
            e.Handled = true;
        }
        else if (point.IsLeftButtonPressed && column.IsDay)
        {
            Focus();
            vm.GoToDay(column);
            e.Handled = true;
        }
    }

    private void OnAffectedClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: CalendarColumn column })
        {
            ViewModel?.OpenAbsence(column);
        }
    }

    /// <summary>Symbol im Spaltenkopf: „Meldungen“ gefiltert auf diese Person bzw. diesen Raum.</summary>
    private void OnColumnAlertClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: CalendarColumn column })
        {
            ViewModel?.OpenMessagesFor(column);
        }
    }

    /// <summary>Die Spalte aus einer Meldung kommt ins Bild: waagrecht so weit scrollen, dass ihr Rand sichtbar ist.</summary>
    private void RevealColumn(double left) => BodyScroll.Offset = new Vector(Math.Max(0, left - 8), BodyScroll.Offset.Y);

    /// <summary>Streifen (Vorbereitung, Nachbereitung, Raumpflege), auf den der Mauszeiger zeigt.</summary>
    private static CalendarStripe? StripeUnder(object? source) =>
        (source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>()
            .FirstOrDefault(c => c.Classes.Contains("stripe"))?.DataContext as CalendarStripe;

    private static CalendarNoteItem? NoteUnder(object? source) => NoteControlUnder(source)?.DataContext as CalendarNoteItem;

    private static Control? NoteControlUnder(object? source) =>
        (source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(c => c.Classes.Contains("sticky"));

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
