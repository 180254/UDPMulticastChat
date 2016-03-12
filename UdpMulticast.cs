using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Kis
{
    /*
     * Nazwa: UdpMulticast
     * Opis: Klasa reprezentująca multicasting po UDP. Realizuje połączenia sieciowe, i przekazuje stan do okna MainWindow.
     * Autor: Adrian Pędziwiatr
     */

    public class UdpMulticast
    {
        private const int ReadBufferSize = 4096;
        private readonly byte[] readBuffer = new byte[ReadBufferSize];
        private readonly MainWindow window;
        private int defaultMulticastNetwork;
        public string DefaultMulticastNetworkName;
        private EndPoint listenEndPoint;
        private Socket listenSocket;
        private MulticastOption multicastOption;
        private EndPoint sendEndPoint;
        private Socket sendSocket;
        /*
           * Nazwa: UdpMulticast (konstruktor)
           * Opis: Konstruktor ustawia referencję do głównego okna, i wstępnie inicjalizuje socket.
           * Argumenty: window - referencja do głównego okna
           * Zwraca: nie dotyczy
           * Używa: brak
           * Modyfikuje: window, (listen|send)socket, (listen|send)endPoint
           * Autor: Adrian Pędziwiatr
           */

        public UdpMulticast(MainWindow window)
        {
            this.window = window;
            InitializeSockets();
        }

        /*
         * Nazwa: InitializeSocket
         * Opis: Inicjalizuje obiekty typu Socket i EndPoint wstawiająć nową referencję.
         * Argumenty: brak
         * Zwraca: void
         * Używa: klasy Socket
         * Modyfikuje: (listen|send)socket, (listen|send)endPoint
         * Autor: Adrian Pędziwiatr
         */

        private void InitializeSockets()
        {
            listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sendSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            listenEndPoint = new IPEndPoint(0, 0);
            sendEndPoint = new IPEndPoint(0, 0);

            UpdateDefaultNetworks();
        }

        /*
         * Nazwa: UpdateDefaultNetworks
         * Opis: Aktualizuje informacje o domyślnym interfejsie używanym przez multicasting.
         * Argumenty: brak
         * Zwraca: void
         * Używa: sendSocket, NetworkInterface
         * Modyfikuje: defaultMulticastNetwork, DefaultMulticastNetworkName
         * Autor: Adrian Pędziwiatr
         */

        private void UpdateDefaultNetworks()
        {
            defaultMulticastNetwork =
                (int)
                    sendSocket.GetSocketOption(SocketOptionLevel.IP,
                        SocketOptionName.MulticastInterface);
            DefaultMulticastNetworkName =
                NetworkInterface.GetAllNetworkInterfaces()[defaultMulticastNetwork].Name;
        }

        /*
         * Nazwa: GetAllListenInterfaces
         * Opis: Funkcja przygotowująca listę interfejsów sieciowych możliwych do nasłuchu w formie możliwiej do wylisowania na kontrolce select.
         * Argumenty: brak
         * Zwraca: NetworkInterfaceUdp[] - tablica interfejsów
         * Używa: klasy NetworkInterface i jej metody statycznej GetAllNetworkInterfaces.
         * Używa: Zwrócone interfejsy zostają opakowane własną klasą NetworkInterfaceUdp
         * Modyfikuje nie dotyczy (static) 
         * Autor: Adrian Pędziwiatr
         */

        public static NetworkInterfaceUdp[] GetAllListenInterfaces()
        {
            List<NetworkInterfaceUdp> networkInterfacesUdpsList = new List<NetworkInterfaceUdp>
            {
                NetworkInterfaceUdp.GetDefaultInstance()
            };

            networkInterfacesUdpsList.AddRange(from networkInterface in NetworkInterface.GetAllNetworkInterfaces()
                where networkInterface.OperationalStatus == OperationalStatus.Up
                select new NetworkInterfaceUdp(networkInterface));

            networkInterfacesUdpsList.RemoveAll(i => i.IpAddress == null);

            return networkInterfacesUdpsList.ToArray();
        }

        /*
         * Nazwa: GetAllListenInterfaces
         * Opis: Funkcja przygotowująca listę interfejsów sieciowych możliwych,
         * Opis: które są poprawne jako interfejsy wysyłające, dla danego interfejsu broadcast,
         * Opis: w  formie możliwiej do wylisowania na kontrolce select.
         * Argumenty: networkInterfaceUdp - interfejs wybrany przez użytkownika
         * Zwraca: NetworkInterfaceUdp[] - tablica interfejsów
         * Używa: klasy NetworkInterface i jej metody statycznej GetAllNetworkInterfaces.
         * Używa: Zwrócone interfejsy zostają opakowane własną klasą NetworkInterfaceUdp
         * Modyfikuje nie dotyczy (static) 
         * Autor: Adrian Pędziwiatr
         */

        public static NetworkInterfaceUdp[] GetAllSendInterfaces(NetworkInterfaceUdp listenInterfaceUdp)
        {
            List<NetworkInterfaceUdp> networkInterfacesUdpsList = new List<NetworkInterfaceUdp>();

            if (listenInterfaceUdp.NetworkInterface == null)
            {
                networkInterfacesUdpsList.AddRange(GetAllListenInterfaces());
                networkInterfacesUdpsList[0].Name = networkInterfacesUdpsList[0].SecondName;
            }
            else
            {
                networkInterfacesUdpsList.Add(listenInterfaceUdp);
            }

            return networkInterfacesUdpsList.ToArray();
        }

        /*
         * Nazwa: PrepareSendIntefaceBoxElements
         * Opis: Przygotowuje nową listę interfejsów sieciowych możliwych do wysłania wiadomości.
         * Argumenty: brak
         * Zwraca: NetworkInterfaceUdp[] - przygotowana lista w postaci tablicy
         * Używa: ListenInterfaceBox
         * Modyfikuje: brak
         * Autor: Adrian Pędziwiatr
         */

        public NetworkInterfaceUdp[] PrepareSendIntefaceBoxElements()
        {
            return
                (from network in GetAllSendInterfaces((NetworkInterfaceUdp) window.ListenInterfaceBox.SelectedItem)
                    select network).ToArray();
        }

        /*
         * Nazwa: UpdateSendSocket
         * Opis: Aktualizuje EndPoint wskazujący na interfejs sieciowy do wysyłania wiadomości multicast.
         * Argumenty: networkInterfaceUdp - interfejs wybrany przez użytkownika
         * Zwraca: brak
         * Używa: brak
         * Modyfikuje: sendEndPoint
         * Autor: Adrian Pędziwiatr
         */

        public void UpdateSendSocket(NetworkInterfaceUdp networkInterfaceUdp)
        {
            int optionValue;
            if (networkInterfaceUdp.NetworkInterface != null)
            {
                IPv4InterfaceProperties properties =
                    networkInterfaceUdp.NetworkInterface.GetIPProperties().GetIPv4Properties();
                optionValue = properties.Index;
            }
            else
            {
                optionValue = defaultMulticastNetwork;
            }
            sendSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
                IPAddress.HostToNetworkOrder(optionValue));
        }

        /*
         * Nazwa: AddSocketMembership
         * Opis: Dodaje do interfejsu nasłuchującego informacje o dołączenie do grupy multicast.
         * Argumenty: multicastAddress - adres grupy multicast
         * Argumenty: network - interfejs sieciowy, dla którego zostanie dodany nasłuch multicast
         * Zwraca: void
         * Używa: klasy MulticastOption
         * Modyfikuje: listenSocket
         * Autor: Adrian Pędziwiatr
         */

        public void AddSocketMembership(IPAddress multicastAddress, NetworkInterfaceUdp network)
        {
            multicastOption = new MulticastOption(multicastAddress, network.IpAddress);
            listenSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, multicastOption);
        }

        /*
          * Nazwa: AsyncConnect
          * Opis: Funkcja inicjalizująca asynchroniczne ropoczęcie transmisji multicast.
          * Argumenty: networkInterfaceUdp - interfejs na którym powinina nastąpić transmisja. 
          * Argumenty: port - nr portu transmisji multicast
          * Zwraca: void
          * Używa: brak
          * Modyfikuje: (listen|send)socket, (listen|send)endPoint
          * Autor: Adrian Pędziwiatr
          */

        public void AsyncConnect(string multicastHost, int port, NetworkInterfaceUdp networkInterfaceUdp)
        {
            lock (listenSocket)
            {
                lock (sendSocket)
                {
                    lock (listenEndPoint)
                    {
                        lock (sendEndPoint)
                        {
                            InitializeSockets();

                            if (networkInterfaceUdp.NetworkInterface == null ||
                                networkInterfaceUdp.NetworkInterface.OperationalStatus == OperationalStatus.Up &&
                                networkInterfaceUdp.IpAddress != null)
                            {
                                try
                                {
                                    InitializeSockets();
                                    NetworkInterfaceUdp[] newSendInterfaceElements = PrepareSendIntefaceBoxElements();

                                    IPAddress multicastAddress = IPAddress.Parse(multicastHost);
                                    sendEndPoint = new IPEndPoint(multicastAddress, port);
                                    listenEndPoint = new IPEndPoint(networkInterfaceUdp.IpAddress, port);
                                    listenSocket.Bind(listenEndPoint);

                                    if (networkInterfaceUdp.NetworkInterface != null)
                                    {
                                        AddSocketMembership(multicastAddress, networkInterfaceUdp);
                                        window.MsgBindSuccess(multicastHost, port, networkInterfaceUdp.Name);
                                    }
                                    else
                                    {
                                        foreach (NetworkInterfaceUdp network in
                                            newSendInterfaceElements.Where(
                                                network => network.NetworkInterface != null))
                                        {
                                            AddSocketMembership(multicastAddress, network);
                                            window.MsgBindSuccess(multicastHost, port, network.Name);
                                        }
                                    }

                                    listenSocket.BeginReceiveFrom(readBuffer, 0, ReadBufferSize, 0,
                                        ref listenEndPoint,
                                        AsyncReceiveCallback, null);

                                    window.UpdateSendInterfaceBox(newSendInterfaceElements);
                                    UpdateSendSocket(networkInterfaceUdp);

                                    window.MsgSuccess();
                                }
                                catch (SocketException ex)
                                {
                                    window.MsgBindError(ex.Message);
                                }
                                catch (FormatException ex)
                                {
                                    window.MsgBindError(ex.Message);
                                }
                            }
                            else
                            {
                                window.MsgBindErrorNetworkIsDown();
                            }
                        }
                    }
                }
            }
        }

        /*
         * Nazwa: AsyncReceiveCallback
         * Opis: Callback wykonywany, gdy na nasłuchiwanym intefejsie pojawi się wiadomość multicast.
         * Opis: Informuje okno główne o otrzymaniu wiadomości.
         * Argumenty: IAsyncResult ar - stan wysyłania asynchronicznego. 
         * Zwraca: void
         * Używa: listenSocket, listenEndPoint, readBuffer
         * Modyfikuje: brak
         * Autor: Adrian Pędziwiatr
         */

        private void AsyncReceiveCallback(IAsyncResult ar)
        {
            lock (listenSocket)
            {
                lock (listenEndPoint)
                {
                    int read;
                    EndPoint sender = new IPEndPoint(0, 0);

                    try
                    {
                        read = listenSocket.EndReceiveFrom(ar, ref (sender));
                    }
                    catch (ObjectDisposedException)
                    {
                        read = 0;
                    }


                    if (read > 0)
                    {
                        string rcvedText = Encoding.UTF8.GetString(readBuffer, 0, read);
                        listenSocket.BeginReceiveFrom(readBuffer, 0, ReadBufferSize, 0, ref listenEndPoint,
                            AsyncReceiveCallback, null);
                        window.MsgReceived(((IPEndPoint) sender).Address.ToString(), rcvedText);
                    }
                }
            }
        }

        /*
         * Nazwa: AsyncSendData
         * Opis: Funkcja rozpoczyna wysyłanie wiadomości multicast w formie tekstowej.
         * Argumenty: data - string do wysłania.
         * Zwraca: void
         * Używa: sendSocket, sendEndPoint
         * Modyfikuje: nie
         * Autor: Adrian Pędziwiatr
         */

        public void AsyncSendData(string msg)
        {
            if (msg.Length == 0)
            {
                return;
            }


            lock (sendSocket)
            {
                lock (sendEndPoint)
                {
                    byte[] dataBytes = Encoding.UTF8.GetBytes(msg);
                    sendSocket.BeginSendTo(dataBytes, 0, dataBytes.Length, 0, sendEndPoint, AsyncSendDataCallback, null);
                }
            }
        }

        /*
         * Nazwa: AsyncSendDataCallback
         * Opis: Callback dla funkcji AsyncSendData. Wywołana zostanie po zrealizowaniu wysyłania.
         * Opis: Kończy proces wysyłania.
         * Argumenty: IAsyncResult ar - stan wysyłania asynchronicznego. 
         * Zwraca: void
         * Używa: sendSocket
         * Modyfikuje: brak
         * Autor: Adrian Pędziwiatr
         */

        private void AsyncSendDataCallback(IAsyncResult ar)
        {
            lock (sendSocket)
            {
                sendSocket.EndSendTo(ar);
            }
        }

        /*
         * Nazwa: AsyncUnbind
         * Opis: Funkcja kończąca nasłuch multicast.
         * Argumenty: brak
         * Zwraca: void
         * Używa: (listen|send)socket
         * Modyfikuje: brak
         * Autor: Adrian Pędziwiatr
         */

        public void AsyncUnbind()
        {
            lock (listenSocket)
            {
                lock (sendSocket)
                {
                    lock (listenEndPoint)
                    {
                        lock (sendEndPoint)
                        {
                            listenSocket.Close();
                            sendSocket.Close();
                            window.MsgUnbind();
                        }
                    }
                }
            }
        }
    }
}