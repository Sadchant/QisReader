﻿using QISReader.Model;
using QisReaderClassLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// Die Elementvorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace QISReader
{
    public class NotenDetailsNavigationArgs
    {
        public NotenDetails NotenDetails { get; }
        public int Id { get; }
        public bool NavigateToCollapsed { get; }

        public NotenDetailsNavigationArgs(NotenDetails notenDetails, int id, bool navigateToCollapsed)
        {
            NotenDetails = notenDetails;
            Id = id;
            NavigateToCollapsed = navigateToCollapsed;
        }
    }
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class NotenPage : Page
    {
        private List<Fach> fachListe;
        private Dictionary<float, SolidColorBrush> notenFarbenDict = new Dictionary<float, SolidColorBrush>();
        private Dictionary<int, string> linkDict;
        private Dictionary<int, NotenDetails> notenDetailsDict;

        private int? currentId; // wird benutzt, um sich die id zu merken, falls auf einen notenspiegel geklickt wurde aber das dict noch nicht gesetzt wurde

        private int spaceAboveFachHeader = 30;
        private int spaceAboveFachInhalt = 5;
        private int aktSpaceAbove;

        private int aktRow = 1; // in aktRow kommt ein FachText rein, steht auf 1 weil auf dem 0ten die Überschrift "Notenübersicht" steht
        

        private const int LEFTCOLUMNWIDTH = 750;

        public NotenPage()
        {
            this.InitializeComponent();
            //testMethode();

            notenFarbenDict.Add(0.0f, (SolidColorBrush)Resources["bestandenBrush"]);
            notenFarbenDict.Add(1.0f, (SolidColorBrush)Resources["sehrgutBrush"]);
            notenFarbenDict.Add(1.3f, (SolidColorBrush)Resources["sehrgutBrush"]);
            notenFarbenDict.Add(1.7f, (SolidColorBrush)Resources["gutBrush"]);
            notenFarbenDict.Add(2.0f, (SolidColorBrush)Resources["gutBrush"]);
            notenFarbenDict.Add(2.3f, (SolidColorBrush)Resources["gutBrush"]);
            notenFarbenDict.Add(2.7f, (SolidColorBrush)Resources["befriedigendBrush"]);
            notenFarbenDict.Add(3.0f, (SolidColorBrush)Resources["befriedigendBrush"]);
            notenFarbenDict.Add(3.3f, (SolidColorBrush)Resources["befriedigendBrush"]);
            notenFarbenDict.Add(3.7f, (SolidColorBrush)Resources["ausreichendBrush"]);
            notenFarbenDict.Add(4.0f, (SolidColorBrush)Resources["ausreichendBrush"]);
            notenFarbenDict.Add(5.0f, (SolidColorBrush)Resources["durchgefallenBrush"]);

            Debug.WriteLine("generate Noten");
            
            
            Debug.WriteLine("Noten generated");
            App.LogicManager.UpdateData.StartDataUpdate += ReactToDataUpdateStart;
            App.LogicManager.UpdateData.DataUpdated += ReactToDataUpdateEnd;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await ShowLastRefresh();
            // die zweite 
            if (Frame.CanGoBack && !Frame.SourcePageType.Equals(typeof(NotenPage)))
            {
                // wird auf Mobile/im Tabletmode nicht beachtet
                Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Visible;
            }
            else
            {
                Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Collapsed;
            }
            if (fachListe == null)
            {
                // FEHLER ABFANGEN!!!
                fachListe = await JsonManager.Load<List<Fach>>(GlobalValues.FILE_NOTEN);
                if (await JsonManager.FileExists(GlobalValues.FILE_NOTENDETAILS))
                    notenDetailsDict = await JsonManager.Load<Dictionary<int, NotenDetails>>(GlobalValues.FILE_NOTENDETAILS);
                else // wenn das notendetailsdict nicht da ist, lade stattdessen das linkdict und hänge methode an das fertig-event, die dann die richtige notenpage läd
                {
                    linkDict = await JsonManager.Load<Dictionary<int, string>>(GlobalValues.FILE_DETAILSDICTIONARY);
                    App.LogicManager.ReadQis.BackgroundTaskFertigEvent += UpdateNotenDetails;
                }       
                generateNotenXaml();
            }
        }

        private async void ReactToDataUpdateStart()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                UpdateProgressRing.Visibility = Visibility.Visible;
            });
        }

        private async void ReactToDataUpdateEnd()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                UpdateProgressRing.Visibility = Visibility.Collapsed;
                await ShowLastRefresh();
            });
        }


        private async Task ShowLastRefresh()
        {
            NotenData notenData = await JsonManager.Load<NotenData>(GlobalValues.FILE_NOTENDATA);
            DateTime lastRefreshTime = notenData.LastRefreshTime;
            StatusTextBlock.Text = "Zuletzt aktualisiert: " + lastRefreshTime.ToString();
        }

        private async void UpdateNotenDetails()
        {

            notenDetailsDict = await JsonManager.Load<Dictionary<int, NotenDetails>>(GlobalValues.FILE_NOTENDETAILS);
            if (currentId != null)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    NavigateToNotenDetails((int)currentId);
                });
            }
        }

        

        private void NotenSpiegelClick(object sender, RoutedEventArgs e)
        {
            // rechte
            RightColumn.Width = new GridLength(LEFTCOLUMNWIDTH);
            // suche anhand der id des senders die richtige DetailsSeite aus dem Dictionary heruas und versende diese
            currentId = (int)((Button)sender).CommandParameter;
            NavigateToNotenDetails((int)currentId);
        }

        private void NavigateToNotenDetails(int id)
        {
            NotenDetails notenDetails = null;
            if (notenDetailsDict != null && notenDetailsDict.ContainsKey(id))
                notenDetails = notenDetailsDict[id];
            //ansonsten bleiben die notendetails null und es wird null mitgegeben

            if (NotenSpiegelFrame.Visibility == Visibility.Collapsed)
            {
                // id wird mitgegeben, damit die Detailspage sich die richtige Seite laden kann, wenn die Notenspiegel fertig ausgelesen worden sind
                this.Frame.Navigate(typeof(NotenDetailsPage), new NotenDetailsNavigationArgs(notenDetails, id, true)); 
            }
            else
            {
                NotenSpiegelFrame.Navigate(typeof(NotenDetailsPage), new NotenDetailsNavigationArgs(notenDetails, id, Frame.BackStack.LastOrDefault() != null));
            }
        }

        private SolidColorBrush getColorFromNote(float? note)
        {
            if (note < 1.0 + 0.15)
            {
                return notenFarbenDict[1.0f];
            }
            else if (note >= 1.0 + 0.15 && note < 1.3 + 0.15)
            {
                return notenFarbenDict[1.3f];
            }
            else if (note >= 1.3 + 0.15 && note < 1.7 + 0.15)
            {
                return notenFarbenDict[1.7f];
            }
            else if (note >= 1.7 + 0.15 && note < 2.0 + 0.15)
            {
                return notenFarbenDict[2.0f];
            }
            else if (note >= 2.0 + 0.15 && note < 2.3 + 0.15)
            {
                return notenFarbenDict[2.3f];
            }
            else if (note >= 2.3 + 0.15 && note < 2.7 + 0.15)
            {
                return notenFarbenDict[2.7f];
            }
            else if (note >= 2.7 + 0.15 && note < 3.0 + 0.15)
            {
                return notenFarbenDict[3.0f];
            }
            else if (note >= 3.0 + 0.15 && note < 3.3 + 0.15)
            {
                return notenFarbenDict[3.3f];
            }
            else if (note >= 3.3 + 0.15 && note < 3.7 + 0.15)
            {
                return notenFarbenDict[3.7f];
            }
            else if (note >= 3.7 + 0.15 && note <= 4.0)
            {
                return notenFarbenDict[4.0f];
            }
            else if (note > 4.0 )
            {
                return notenFarbenDict[5.0f];
            }
            return new SolidColorBrush(Colors.Gray);
        }

        private void addToNotenGrid(FrameworkElement addThis, int row, int column)
        {
            NotenGrid.Children.Add(addThis);
            Grid.SetRow(addThis, row);
            Grid.SetColumn(addThis, column);
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        

        // kommt später weg
        //private void InfoButton_Click(object sender, RoutedEventArgs e)
        //{
        //    var bounds = ApplicationView.GetForCurrentView().VisibleBounds;
        //    var scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
        //    var size = new Size(bounds.Width * scaleFactor, bounds.Height * scaleFactor);

        //    BoundsText.Text = bounds.ToString();
        //    ScaleFactorText.Text = scaleFactor.ToString();
        //    SizeText.Text = size.ToString();
        //}
        //private async void testMethode()
        //{
        //    /*Scraper2 testScraper = new Scraper2();
        //    string html = await testScraper.ClientScraper();*/
        //    FileReader fileReader = new FileReader();
        //    string html = await fileReader.readNotenPage();
        //    List<String> notenStringList = App.LogicManager.NotenParser.parseNoten(html);
        //    App.LogicManager.FachManager.buildFachObjektList(notenStringList);

        //    string notenSpiegelPage = await FileReader.readNotenSpiegelPage();
        //    App.LogicManager.NotenParser.parseNotenSpiegel(notenSpiegelPage);
        //}


        // die App soll nur "Versuch 1" anzeigen, wenn es einen zweiten Versuch gibt oder ein Fach nicht bestanden wurde und es noch keinen zweiten Versuch gibt
        // diese Funktion gibt ein Array aus booleans zurück, die so lang ist wie die Fachliste mit trues an der Stelle, wo der Versuch angezeigt werden soll
        public bool[] GetVersucheToShow(List<Fach> fachListe)
        {
            bool[] versucheToShow = new bool[fachListe.Count];

            if (fachListe == null || fachListe.Count == 0)
            {
                Debug.WriteLine("Liste ist leer, Funktion zu früh aufgerufen!");
                return null;
            }
            int i = 0, j = 0;
            foreach (Fach aktFach in fachListe)
            {
                if (aktFach is FachInhalt)
                {
                    FachInhalt aktFachInhalt = (FachInhalt)aktFach;
                    if (aktFachInhalt.Versuch > 1) // wenn der Versuch 2 oder größer ist, soll auf jeden Fall "Versuch 2" ... angezeigt werden
                    {
                        versucheToShow[i] = true; // also setze den aktuellen Index auf true
                        j = 0;
                        foreach (Fach secondAktFach in fachListe) // und den dazugehörigen ersten Versuch suchen
                        {
                            if (secondAktFach is FachInhalt)
                            {
                                if (string.Equals(((FachInhalt)secondAktFach).FachName, aktFachInhalt.FachName)) // wenn die Fächer gleich heißen
                                {
                                    versucheToShow[j] = true;
                                }
                            }
                            j++;
                        }
                    }
                    if ((aktFachInhalt.Bestanden != null) && (!(bool)aktFachInhalt.Bestanden)) // wenn es der erste Versuch war, aber man nicht bestanden hat, setze auch auf true
                    {
                        versucheToShow[i] = true;
                    }
                }
                i++;
            }

            return versucheToShow;
        }

        private void generateNotenXaml()
        {
            // wenn nichts drin war ist was schief gelaufen, sollte nicht vorkommen!
            if (fachListe.Count == 0)
            {
                Debug.WriteLine("Achtung! FachListe ist leer!");
                return;
            }
            int i = 0; // nur für die Anzeige der Versuche da
            bool[] notenToShow = GetVersucheToShow(fachListe); // hole Liste um nur die Versuche anzuzeigen, die interessant sind
            bool isFirst = true;

            FachInhalt aktFachInhalt; // damit nicht in jedem foreach-Durchlauf ein Objekt erzeugt werden muss ;)

            foreach (Fach aktFach in fachListe)
            {
                if (aktFach is FachInhalt)
                {
                    aktFachInhalt = (FachInhalt)aktFach;
                    aktSpaceAbove = spaceAboveFachInhalt;


                    // das Rect für die Hintergrundfarbe
                    // zuerst Farbe festlegen
                    SolidColorBrush backgroundColor;
                    if (aktFachInhalt.Note != null && aktFachInhalt.Note > 0) // Note
                    {
                        backgroundColor = getColorFromNote(aktFachInhalt.Note);
                    }
                    else if (aktFachInhalt.Bestanden != null) // sollte Bestanden null sein bleibt die Farbe Grau
                    {
                        if ((bool)aktFachInhalt.Bestanden)
                            backgroundColor = notenFarbenDict[0.0f]; // 0.0 ist der Key für das bestanden-Blau
                        else
                            backgroundColor = notenFarbenDict[5.0f]; // wenn nicht bestanden nutze das 5.0-Rot
                    }
                    else
                        backgroundColor = new SolidColorBrush(Colors.LightGray);


                    Rectangle testRect = new Rectangle { Fill = backgroundColor };
                    addToNotenGrid(testRect, aktRow, 0);
                    Grid.SetColumnSpan(testRect, 11);

                    if (aktFachInhalt.FachName != null) // Fachname
                    {
                        TextBlock fachText = new TextBlock { Text = aktFachInhalt.FachName, FontSize = 18, /*Margin = new Thickness(40, 0, 0, 0),*/ TextWrapping = TextWrapping.Wrap, MaxWidth = 480, /*FontStyle = FontStyle.Italic*/ };
                        addToNotenGrid(fachText, aktRow, 1);
                    }
                    if (aktFachInhalt.Note != null) // Note
                    {
                        TextBlock notenText = new TextBlock { Text = ((float)aktFachInhalt.Note).ToString("0.0"), FontSize = 18, HorizontalAlignment = HorizontalAlignment.Right, /*FontStyle = FontStyle.Italic*/ };
                        addToNotenGrid(notenText, aktRow, 3);
                    }
                    if (aktFachInhalt.Versuch != null) // Versuch
                    {
                        if (notenToShow[i]) // die trues im bool-Array notenToShow wurden durch getNotenToShow() an den gewünschten Stellen auf true gesetzt
                        {
                            TextBlock versuchText = new TextBlock { Text = "Versuch " + aktFachInhalt.Versuch.ToString(), FontSize = 18, HorizontalAlignment = HorizontalAlignment.Right, /*FontStyle = FontStyle.Italic*/ };
                            addToNotenGrid(versuchText, aktRow, 5);
                        }

                    }
                    if (aktFachInhalt.Cp != null) // Cp
                    {
                        TextBlock cpText = new TextBlock { Text = ((float)aktFachInhalt.Cp).ToString("0.0") + " Cp", FontSize = 18, HorizontalAlignment = HorizontalAlignment.Right, /*FontStyle = FontStyle.Italic*/ };
                        addToNotenGrid(cpText, aktRow, 7);
                    }
                    int id = -1;
                    if (aktFachInhalt.Id != null) id = (int)aktFachInhalt.Id;

                    // wenn notenDetails null sind, schaue im linkDict, da die details noch am laden sind
                    if ((notenDetailsDict != null && notenDetailsDict.ContainsKey(id)) || (notenDetailsDict == null && linkDict.ContainsKey(id)))
                    {
                        Button notenSpiegelButton = new Button { Content = "Notenspiegel", Padding = new Thickness(0), Margin = new Thickness(0, 0, 15, 0), CommandParameter = aktFachInhalt.Id };
                        notenSpiegelButton.Click += NotenSpiegelClick;
                        addToNotenGrid(notenSpiegelButton, aktRow, 9);
                    }
                    //NotenGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }
                else //ansonsten ist es ein Fach
                {
                    aktSpaceAbove = spaceAboveFachHeader;

                    // das Rect für die Hintergrundfarbe
                    // zuerst Farbe festlegen
                    SolidColorBrush backgroundColor;
                    if (aktFach.Note != null && aktFach.Note > 0) // Note
                    {
                        backgroundColor = getColorFromNote(aktFach.Note);
                    }
                    else if (aktFach.Bestanden != null)
                    {
                        if ((bool)aktFach.Bestanden)
                            backgroundColor = notenFarbenDict[0.0f]; // 0.0 ist der Key für das bestanden-Blau
                        else
                            backgroundColor = notenFarbenDict[5.0f]; // wenn nicht bestanden nutze das 5.0-Rot
                    }
                    else
                        backgroundColor = new SolidColorBrush(Colors.LightGray);
                    Rectangle testRect = new Rectangle { Fill = backgroundColor };
                    addToNotenGrid(testRect, aktRow, 0);
                    Grid.SetColumnSpan(testRect, 11);

                    if (aktFach.FachName != null) // Fachname
                    {
                        TextBlock fachText = new TextBlock { Text = aktFach.FachName, FontSize = 20, /*Margin = new Thickness(20, 0, 0, 0),*/ FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, MaxWidth = 480, HorizontalAlignment = HorizontalAlignment.Left };
                        addToNotenGrid(fachText, aktRow, 1);
                    }
                    if (aktFach.Note != null) // Note
                    {
                        TextBlock notenText = new TextBlock { Text = ((float)aktFach.Note).ToString("0.0"), FontSize = 20, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right };
                        addToNotenGrid(notenText, aktRow, 3);
                    }
                    if (aktFach.Cp != null) // Cp
                    {
                        TextBlock cpText = new TextBlock { Text = ((float)aktFach.Cp).ToString("0.0") + " CP", FontSize = 20, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right };
                        addToNotenGrid(cpText, aktRow, 7);
                    }
                }
                
                aktRow += 2;
                // wenns nicht der erste ist, mache eine Leerzeile
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    NotenGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(aktSpaceAbove) });
                }
                NotenGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                i++;
            }

            //NotenGrid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            //Rectangle aktRect = new Rectangle { Width = 1000, Height=100, Fill=new SolidColorBrush(Colors.Red) } ;
            //NotenGrid.Children.Add(aktRect);
            //Grid.SetRow(aktRect, 1);
        }
    }
}
