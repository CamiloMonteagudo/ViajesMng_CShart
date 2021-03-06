﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static MngViajes.DataBase;

namespace MngViajes
  {
  public class Viaje
    {
    public string DBFile;             // Localizacion del archivo donde se define los datos del viaje
    public string Code;               // Codigo del nombre del viaje
    public DataBase BD;
    public Money MD;

    public PresupuestoDataTable tablePresupesto;
    public GastosDataTable      tableGastos;
    public ComprasDataTable     tableCompras;
    public VentasDataTable      tableVentas;
    public PagosDataTable       tablePagos;

    public string Title;                    // Nombre del viaje
    public string TitleShort;               // Nombre del viaje abreviada

    public decimal sumaUSD;                 // Suma de dinero invertido en USD
    public decimal sumaCUC;                 // Suma de dinero invertido en CUC

    public decimal totalUSD;                // Suma total de todo el dinero invertido convertido a USD
    public decimal totalCUC;                // Suma total de todo el dinero invertido convertido a CUC

    public decimal GastosCUC;               // Dinero gastado para garantizar la inversión
    public decimal CompasCUC;               // Dinero Invertido en la compa de mercancias
    public decimal RecupIdx;                // Indice de recuperación de la inversión
    public decimal MontoInvers;             // Monto total de la inversion

    public decimal MontoPrecios;            // Monto total según los precios estimados
    public decimal GanancPrecios;           // Ganacia sesun los precios

    public decimal MontoCobros;             // Cantidad total de dinero que ha sido cobrado
    public decimal MontoVentas;             // Moto total si se ejecutan todoas las ventas
    public decimal GanacVentas;             // Ganacia si se ejecutan todas las ventas
    public decimal MontoConsumo;            // Costo de todos los item que se usaron para consumo
    public decimal MontoConsumoRecp;        // Costo de recueperación de todos los item que se usaron para consumo
    public decimal GanacConsumo;            // Ganancia producto de los item que se consumen
    public decimal NumChgPrecio;            // Número de items que se les cambia el precio
    public decimal MontoChgPrecio;          // Cantidad de dinero involucrado en el cambio de precio
    public decimal NumDevoluc;              // Número de items sin vender
    public decimal MontoDevoluc;            // Monto de todos los items sin vender
    public decimal NumSinPagar;             // Número de items sin pagar
    public decimal MontoSinPagar;           // Monto de todos los items sin pagar
    public decimal NumSinVender;            // Número de items sin vender
    public decimal MontoSinVender;          // Monto de todos los items sin vender

    public string[] Vendedores = { "Consumo" };

    int IdxV;                               // Indice del viaje dentro de la lista de viajes
    //--------------------------------------------------------------------------------------------------------------------------------------
    public Viaje( int idx, string code, string file )
      {
      IdxV   = idx; 
      Code   = code;
      DBFile = file;
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    public bool Load()
      {
      Configuration();

      BD = new DataBase();
      BD.DataSetName = "DataBase";
      BD.SchemaSerializationMode = SchemaSerializationMode.IncludeSchema;

      try
        {
        BD.ReadXml( DBFile );

        tablePresupesto = BD.Presupuesto;
        tableGastos     = BD.Gastos;
        tableCompras    = BD.Compras;
        tableVentas     = BD.Ventas;
        tablePagos      = BD.Pagos;

        UpdateEstadisticas();
        return true;
        }
      catch( Exception )
        {
        return false;
        }
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    // Carga el fichero de configuracion del viaje y extrae la información
    public void Configuration()
      {
      string fName = DBFile.ToLower().Replace( ".xml", ".ini" );

      if( !File.Exists( fName ) ) return;

      decimal UsdToCuc=1, CupToCuc=1, UsdToDop=1;

      var lines = File.ReadAllLines(fName);

      char[] sepDosPnts = { ':' };
      foreach( var line in lines )
        {
        var Parts = line.Split( sepDosPnts, 2 );
        if( Parts.Length != 2 ) continue;

        var sVal = Parts[1].Trim();
        switch( Parts[0] )
          {
          case "Vededores": Vendedores = sVal.Split( ',' ); break;
          case "UsdToCuc" : decimal.TryParse( sVal, out UsdToCuc ); break;
          case "CupToCuc" : decimal.TryParse( sVal, out CupToCuc ); break;
          case "UsdToDop" : decimal.TryParse( sVal, out UsdToDop ); break;
          case "Titulo"   : Title      = sVal; break;
          case "Titulo2"  : TitleShort = sVal; break;
          }
        }

      MD = new Money(UsdToCuc, CupToCuc, UsdToDop );
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Actualiza todas las varibles de las estadisticas del viaje</summary>
    public void UpdateEstadisticas()
      {
      GetPresupuesto();
      GetGastos();
      GetCompras();
      GetVentas();
      CobrosSumary();
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Obtiene los datos generales sobre el presupuesto</summary>
    public void GetPresupuesto()
      {
      sumaUSD = sumaCUC = totalUSD = totalCUC = 0;

      foreach( PresupuestoRow row in tablePresupesto.Rows )
        {
        var cambio = row.cambio;
        var moneda = (Mnd)row.moneda;
        var value  = row.value;

        if( moneda == Mnd.Usd )
          {
          sumaUSD  += value;
          totalUSD += value;
          totalCUC += ( value * cambio );
          }
        else
          {
          sumaCUC  += value;
          totalCUC += value;
          totalUSD += ( value / cambio );
          }
        }

      if( totalUSD>0 && totalCUC>0 )
        MD.UsdToCuc = totalCUC/totalUSD;
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Obtiene los datos generales sobre los gastos</summary>
    public void GetGastos()
      {
      GastosCUC = 0;

      foreach( GastosRow row in tableGastos.Rows )
        GastosCUC += row.cuc;

      MontoInvers    = GastosCUC + CompasCUC;
      RecupIdx = ( CompasCUC>0 ) ? ( MontoInvers/CompasCUC ) : MontoInvers;
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Obtiene los datos generales sobre las Compras</summary>
    public void GetCompras()
      {
      CompasCUC = 0;
      MontoPrecios  = 0;
      GanancPrecios = 0;

      foreach( ComprasRow row in tableCompras.Rows )
        {
        var mond = (Mnd)row.moneda;
        var prec = row.precio;
        var cant = row.count;

        if( mond != Mnd.Cuc )
          prec = MD.Convert( prec, mond, Mnd.Cuc );

        MontoPrecios += ( prec * cant );
        CompasCUC    += row.valCUC;
        }

      MontoInvers   = GastosCUC + CompasCUC;
      GanancPrecios = MontoPrecios - MontoInvers;

      RecupIdx = ( CompasCUC>0 ) ? ( MontoInvers/CompasCUC ) : MontoInvers;
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Obtiene los datos generales sobre los cobros realizados</summary>
    internal void CobrosSumary()
      {
      MontoCobros = 0;
      foreach( PagosRow row in tablePagos )
        {
        var Pagado = row.cuc;
        Pagado += MD.Convert( row.cup, Mnd.Cup, Mnd.Cuc );

        MontoCobros += Pagado;
        }
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Obtiene los datos generales sobre las compras</summary>
    public static Regex reMark = new Regex(@" Devolvio ([0-9]+)\}", RegexOptions.Compiled);
    public void GetVentas()
      {
      Dictionary<Int32,Int32> GroupVentas = new Dictionary<Int32,Int32>();

      MontoVentas = GanacVentas = MontoConsumo = GanacConsumo = MontoConsumoRecp = NumChgPrecio = MontoChgPrecio = NumDevoluc = MontoDevoluc = NumSinPagar = MontoSinPagar = 0;

      foreach( VentasRow row in tableVentas )
        {
        var Cant   = row.count;
        var idProd = row.idProd;

        if( !GroupVentas.ContainsKey( idProd ) ) GroupVentas[idProd]  = Cant;
        else GroupVentas[idProd] += Cant;

        var rowProd = tableCompras.FindByid( idProd );
        if( rowProd == null ) continue;

        var montoProd = MD.Convert( Cant*rowProd.precio, (Mnd)rowProd.moneda, Mnd.Cuc);

        if( row.vendedor == Vendedores[0] )           // Item para consumo
          {
          var costo    = Cant * rowProd.valCucItem;
          var costoRcp = costo * RecupIdx;

          //Debug.WriteLine( "idProd=" + idProd + " Cant=" + Cant + " Precio=" + montoProd.ToString( "#.##" ) );

          MontoConsumo     += costo;
          MontoConsumoRecp += costoRcp;
          GanacConsumo     += ( montoProd-costoRcp );
          continue;
          }

        var precioVenta = MD.Convert( row.precio, (Mnd)row.moneda, Mnd.Cuc);
        var montoVenta  = Cant * precioVenta;

        MontoVentas += montoVenta;

        if( montoProd != montoVenta )
          {
          NumChgPrecio += Cant;
          MontoChgPrecio += ( montoVenta - montoProd );

          //Debug.WriteLine( "ID:" + row.id + " Precio:" + montoProd  + " por:" + montoVenta  + " Dif:" + ( montoVenta - montoProd ) );
          }

        if( !row.IscomentarioNull() )
          {
          var matches = reMark.Matches(row.comentario);
          foreach( Match match in matches )
            {
            var Num = int.Parse(match.Groups[1].Value);

            NumDevoluc   += Num;
            MontoDevoluc += ( Num * precioVenta );
            }
          }

        var Pago = GetCucPagado( row );
        var SinPagar = montoVenta - Pago;

        if( precioVenta!=0 ) NumSinPagar += SinPagar/precioVenta;
        MontoSinPagar += SinPagar;
        }

      GanacVentas = MontoVentas - MontoInvers;

      NumSinVender = MontoSinVender = 0;
      foreach( ComprasRow row in tableCompras )
        {
        var idProd = row.id;
        var Cant   = row.count;

        var Resto = Cant;
        if( GroupVentas.ContainsKey( idProd ) ) Resto -= GroupVentas[idProd];

        if( Resto <= 0 ) continue;

        var Precio = MD.Convert( row.precio, (Mnd)row.moneda, Mnd.Cuc );

        NumSinVender   += Resto;
        MontoSinVender += ( Resto*Precio );
        }
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Obtiene la cantidad pagada para la venta especificada por 'row'</summary>
    public decimal GetCucPagado( VentasRow row )
      {
      var relac = BD.Relations["Ventas_Pagos"];

      decimal pagado = 0;
      PagosRow[] Pagos = (PagosRow[])row.GetChildRows(relac);
      foreach( PagosRow rowPago in Pagos )
        {
        var pago = rowPago.cuc;
        pago +=  MD.Convert( rowPago.cup, Mnd.Cup, Mnd.Cuc );

        pagado += pago;
        }

      return pagado;
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    Dictionary<Int32,Int32> Counts = new Dictionary<Int32,Int32>();
    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Llena la lista de selección de productos</summary>
    public void FillTableProdsSinVender( DataTable table, string sFilter )
      {
      CalculateCounts();

      var ratioRecp = 1m;
      var ratioGanc = 1m;
      if( CompasCUC != 0 )
        {
        ratioRecp = MontoInvers / CompasCUC;      // Relación entre en monto de la inversión y el de las compras
        ratioGanc =  (1.5m * MontoInvers) / CompasCUC;
        }

      foreach( ComprasRow row in tableCompras )
        {
        var idProd = row.id;
        if( App.FilterFor(-1, idProd) ) continue;

        var Item   = row.item;
        var Cant   = row.count;
        var Precio = row.precio;
        Mnd Moned  = (Mnd)row.moneda;
        var sMond  = MD.Code( Moned );
        var valCosto = MD.Convert( row.valCucItem, Mnd.Cuc, Moned );
        var valRecp = ratioRecp * valCosto;
        var valGanc = ratioGanc * valCosto;

        var Resto = Cant;
        if( Counts.ContainsKey( idProd ) ) Resto -= Counts[idProd];

        if( Resto<=0 ) continue;
        if( !FindFilter( sFilter, Item ) ) continue;

        var monto = Resto*Precio;
        if( Moned!=Mnd.Cuc && Moned!=Mnd.Cup )
          {
          monto = MD.Convert( monto, Moned, Mnd.Cuc );
          Moned = Mnd.Cuc;
          }

        var      n  = table.Rows.Count + 1;
        var  sCant  = Resto.ToString() + " | " + Cant;
        var  sCosto = valCosto.ToString("0.##");
        var  sRecup = valRecp.ToString("0.##");
        var sGananc = valGanc.ToString("0.##");

        var sPrecio = Precio.ToString("0.##") + ' ' + sMond;
        var sMonto  = monto.ToString("0.##") + ' ' + sMond;
        var Total   = (Moned==Mnd.Cuc)? monto : MD.Convert( monto, Moned, Mnd.Cuc );

        if( Resto==1 ) sMonto = "";

        //          idxViaje, Num, Viaje,     ID, Item,  Cant,   Costo, CostoRec, CostoOK, Precio,   Monto, Total
        //               int, int,   Str,    Str,  Str,   Str,     Str,      Str,     Str,    Str,     Str,   dec
        table.Rows.Add( IdxV,   n, Title, idProd, Item, sCant,  sCosto,   sRecup, sGananc, sPrecio, sMonto, Total );
        }
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Busca si la cadena 'sFilter' esta conenida en la cadena 'str'</summary>
    private bool FindFilter( string sFilter, string str )
      {
      if( string.IsNullOrEmpty(sFilter) ) return true;

      return str.ToLower().Contains( sFilter.ToLower() );
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Calcula las cantidades disponible de cada tipo de item</summary>
    private void CalculateCounts()
      {
      Counts.Clear();

      foreach( VentasRow row in tableVentas )
        {
        var idProd = row.idProd;
        var Cant   = row.count;

        if( !Counts.ContainsKey( idProd ) ) Counts[idProd]  = Cant;
        else Counts[idProd] += Cant;
        }
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Llena la tabla de ventas sin cobrar</summary>
    public void FillVentasSinCobrar( DataTable table, string sFilter, string fVendor )
      {
      sFilter = sFilter.ToLower().Trim();
      fVendor = fVendor.ToLower();

      foreach( VentasRow row in tableVentas )
        {
        if( App.FilterFor(-1, row.idProd, row.id) ) continue;

        var vVendor = row.vendedor.ToLower();
        if( fVendor.Length>0 && fVendor!=vVendor ) continue;

        var precio = row.precio;
        var Pagado = GetPagado( row );
        var Monto  = row.count*precio;

        if( Pagado>=Monto ) continue;

        var sItem = "No se encontro el Item";
        var rowProd = tableCompras.FindByid( row.idProd );
        if( rowProd != null )
          sItem = rowProd.item;

        if( !row.IscomentarioNull() && row.comentario.Trim().Length>0 )
          sItem += " ▶ " + row.comentario;

//        sItem += " | " + TitleShort;

        if( sFilter.Length>0 && !sItem.ToLower().Contains( sFilter ) && sFilter != vVendor )
          continue;

        var ItemPago = 0m;
        if( precio!=0 ) ItemPago = Pagado/precio;

        var Moned  = (Mnd)row.moneda;
        var sMoned = MD.Code( Moned );
        var sPrecio = precio.ToString("0.##") + ' ' + sMoned;

        var sPagado = "";
        if( Pagado>0 )
          sPagado = Pagado.ToString( "0.##" ) + ' ' + sMoned + " = " + ItemPago.ToString( "0.##" );

        var resto       = row.count-ItemPago;
        var porPagar    = resto * precio;
        var porPagarCuc = MD.Convert( porPagar, Moned, Mnd.Cuc );

        var sCant = resto.ToString("0.#");
        if( resto != row.count ) sCant += " | " + row.count;

        var sMonto = "";
        if( resto != 1 )
          sMonto = porPagar.ToString("0.##") + ' ' + sMoned;

        var n = table.Rows.Count+1;
        table.Rows.Add( IdxV, n, row.id, TitleShort, sItem, row.vendedor, sCant, sPrecio, sMonto, porPagarCuc, sPagado, row.idProd );
        }
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Obtiene la cantidad pagada para la venta especificada por 'row'</summary>
    private decimal GetPagado( VentasRow row )
      {
      var mond  = (Mnd)row.moneda;
      var relac = BD.Relations["Ventas_Pagos"];

      decimal pagado = 0;
      PagosRow[] Pagos = (PagosRow[])row.GetChildRows(relac);
      foreach( PagosRow rowPago in Pagos )
        {
        if( rowPago.cuc > 0 )
          {
          var PagoCuc = rowPago.cuc;
          if( mond != Mnd.Cuc )
            PagoCuc = MD.Convert( PagoCuc, Mnd.Cuc, mond );

          pagado += PagoCuc;
          }

        if( rowPago.cup > 0 )
          {
          var PagoCup = rowPago.cup;
          if( mond != Mnd.Cup )
            PagoCup = MD.Convert( PagoCup, Mnd.Cup, mond );

          pagado += PagoCup;
          }
        }

      return pagado;
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Llena el Grid con los pagos realizados</summary>
    public void FillTablePagos( DataTable table, string sFilter, string fVendor )
      {
      sFilter = sFilter.ToLower().Trim();
      fVendor = fVendor.ToLower();

      foreach( PagosRow row in tablePagos )
        {
        var IdVent = row.idVent;

        var sItem = "Nombre del Item sin determinar";
        var sVend = "Desconcido";
        var sPrec = "";
        var idProd = -1;

        var rowVent = tableVentas.FindByid( IdVent );
        if( rowVent != null )
          {
          sVend = rowVent.vendedor;
          sPrec = rowVent.precio.ToString( "0.##" ) + ' ' + MD.Code( (Mnd)rowVent.moneda );

          idProd  = rowVent.idProd;
          var rowProd = tableCompras.FindByid( idProd );
          if( rowProd != null )
            sItem = rowProd.item;
          }

        if( App.FilterFor(-1, idProd, IdVent ) ) continue;

        if( fVendor.Length>0 && fVendor!=sVend.ToLower() ) continue;

        if( !row.IscomentarioNull() && row.comentario.Trim().Length>0 )
            sItem += " ▶ " + row.comentario + ' ';

        if( sFilter.Length>0 && !sItem.ToLower().Contains( sFilter ) && sFilter != sVend.ToLower() )
          continue;

        var num = table.Rows.Count+1;
        var sum = row.cuc + MD.Convert( row.cup, Mnd.Cup, Mnd.Cuc );

                 // idxViaje, Num, IdPago, Viaje,      Item,  Vend,  Cant,     Precio, Cuc,     Cup,   Total, Fecha    ,IdProd, IdVent 
        table.Rows.Add( IdxV, num, row.id, TitleShort, sItem, sVend, row.count, sPrec, row.cuc, row.cup, sum, row.fecha, idProd, IdVent );
        }
      }

    const int ShowComent = 0x01;
    const int ShowPago   = 0x02;
    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Llena el Grid con las ventas realizadas</summary>
    public void FillTableVentas( DataTable table, string sFilter, string fVendor, int sw )
      {
      sFilter     = sFilter.ToLower().Trim();
      fVendor     = fVendor.ToLower();
      var consumo = Vendedores[0];

      foreach( VentasRow row in tableVentas )
        {
        if( App.FilterFor(-1, row.idProd, row.id) ) continue;

        var vVendor = row.vendedor;
        if( fVendor.Length>0 && fVendor!=vVendor.ToLower() ) continue;

        var Monto   = row.count * row.precio;
        if( row.count<0 && Monto>0 ) Monto = -Monto;

        var  Moneda = (Mnd)row.moneda;
        var sMoneda = MD.Code( Moneda );

        var sItem = "Nombre desconocido";
        var ItemRow = tableCompras.FindByid( row.idProd );
        if( ItemRow != null )
          sItem = ItemRow.item;

        if( (sw&ShowComent)!=0 && !row.IscomentarioNull() && row.comentario.Trim().Length>0 )
          sItem += " ▶ " + row.comentario + ' ';

        var cant = row.count;
        if( ( sw&ShowPago )!=0 )
          {
          if( vVendor!="Consumo" )
            { 
            var Pago = GetPagado( row );
            if( Pago>=Monto ) 
              {
              if( cant>0 ) sItem += " ▶ Pagado" ;
              else         sItem += " ▶ Devuelto";
              }
            else
              {
              if( Pago > 0 )  sItem += " ▶ " + Pago + ' ' + sMoneda + ' ';
              }
            }
          else
            sItem += " ▶ Consumo";
          }


        if( sFilter.Length>0 && !sItem.ToLower().Contains( sFilter ) && sFilter != vVendor.ToLower() )
          continue;

        var num = table.Rows.Count+1;
        var Cuc = (Moneda==Mnd.Cuc)? Monto : 0;
        var Cup = (Moneda==Mnd.Cup)? Monto : 0;
        var sum = Cuc + MD.Convert( Cup, Mnd.Cup, Mnd.Cuc );

        //          idxViaje, Num,IdVent , Viaje     , Item , Vend   , Cant     , Precio    , Cuc,Cup,Total,Fecha, IdProd 
        table.Rows.Add( IdxV, num, row.id, TitleShort, sItem, vVendor, cant, row.precio, Cuc, Cup, sum, row.fecha, row.idProd );
        }
      }

    const int ShowRawGananc  = 0x04;
    const int ShowItemGananc = 0x08;
    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Llena el Grid con las los productos en el sistema</summary>
    internal void FillTableProducts( DataTable table, string sFilter, int sw )
      {
      sFilter = sFilter.ToLower().Trim();

      foreach( ComprasRow row in tableCompras )
        {
        if( App.FilterFor(-1, row.id ) ) continue;

        var sItem = row.item;

        if( ( sw&ShowComent )!=0 && !row.IscomentarioNull() && row.comentario.Trim().Length>0 )
          sItem += " ▶ " + row.comentario + ' ';

        if( sFilter.Length>0 && !sItem.ToLower().Contains( sFilter ) )
          continue;

        var Mond = (Mnd)row.moneda;
        var Prec = row.precio;
        var Cant = row.count;
        var Monto = Prec*Cant;
        var PrecCuc = MD.Convert( Prec, Mond, Mnd.Cuc );
        var MontoCuc = PrecCuc * Cant;                                // Valor del venta completa (en cuc)

        var sMonto  = Monto.ToString("0.##");
        if( Monto != 0 ) sMonto += ' ' + MD.Code( Mond );

        var sPrecio = row.precio.ToString("0.##");
        if( Prec != 0 ) sPrecio += ' ' + MD.Code( Mond );

        var ratioRecp = 1m;                                           // Para precios brutos (sin recuperació)  
        if( CompasCUC != 0 && ( sw&ShowRawGananc )==0 )
          ratioRecp = MontoInvers / CompasCUC;                        // Relación entre en monto de la inversión y el de las compras

        var PrecRecp  = ratioRecp * row.valCucItem;                   // Precio de recueración de la inversión

        var Rate = Prec;
        if( PrecRecp!=0 ) Rate = ( PrecCuc/PrecRecp );                // Relación entre el precio y el precio de recuperación

        var nItem = ((sw&ShowItemGananc)!=0)? 1 : Cant;               // Ganancia por item
        var Ganc  = ( nItem*PrecCuc ) - (nItem*PrecRecp );            // Ganancia neta

        var num = table.Rows.Count+1;

        var Value = row.value;
        if( ( sw&ShowItemGananc )!=0  )
          Value = row.valItem;

        //idxViaje,Num,idItem,Viaje,item,                    count, value     ,valCUC         ,precio  ,monto  ,montoCUC,rate,ganancia
        table.Rows.Add( IdxV, num, row.id, TitleShort, sItem, Cant, Value, row.valCucItem, sPrecio, sMonto, MontoCuc, Rate, Ganc );
        }

      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Obtiene una corta descripción con los datos principales del porducto 'IdProd'</summary>
    internal string ProdDesc( int IdProd )
      {
      var rowProd = tableCompras.FindByid( IdProd );
      if( rowProd == null ) return "";

      var sItem = rowProd.item;
      var cant  = rowProd.count;
      var prec  = rowProd.precio;
      var sMnd  = MD.Code( (Mnd)rowProd.moneda );

      return sItem + " [" + cant + " items a " + prec.ToString("0.##") + sMnd + ']';
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    /// <summary>Obtiene una corta descripción con los datos principales de la venta 'IdVent'</summary>
    internal string VentDesc( int IdVent )
      {
      var rowVent = tableVentas.FindByid( IdVent );
      if( rowVent == null ) return "";

      var cant  = rowVent.count;
      var prec  = rowVent.precio;
      var sMnd  = MD.Code( (Mnd)rowVent.moneda );

      return rowVent.vendedor + " [" + cant + " items a " + prec.ToString("0.##") + sMnd + ']';
      }

    //--------------------------------------------------------------------------------------------------------------------------------------
    }
  }
