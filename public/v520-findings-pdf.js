(function(global){
  "use strict";
  var logoPromise;

  function text(value,fallback){
    var result=String(value==null?"":value).trim();
    return result||fallback||"No registrado.";
  }

  function normalize(value){return String(value||"").normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLowerCase();}
  var fieldQuestion="¿Hubo alguna avería durante el levantamiento?";
  var origins={
    NOT_PAINTED:["¿La agencia fue pintada?","No"],NO_RAZA_STICKER:["¿Fue colocado el sticker de raza?","No"],NO_REAL_STICKER:["¿Fue colocado el sticker de Real?","No"],LOTEKA_NOT_REMOVED:["¿Fue retirada toda la publicidad de Loteka?","No"],PRINTER_NO_MAINTENANCE:["¿Le dieron mantenimiento a la impresora?","No"],NO_INVERTER:["¿Tiene inversor?","No"],NO_BATTERY:["¿Tiene batería?","No"],
    DAMAGE_WIRING:[fieldQuestion,"Sí - Fallas de cableado"],DAMAGE_SCREENS:[fieldQuestion,"Sí - Pantallas o monitores"],DAMAGE_CONNECTIVITY:[fieldQuestion,"Sí - Conectividad"],DAMAGE_FURNITURE:[fieldQuestion,"Sí - Reestructuración física / Sheetrock"],DAMAGE_PAINTING:[fieldQuestion,"Sí - Pintura"],DAMAGE_INVERTER:[fieldQuestion,"Sí - Inversor"],DAMAGE_BATTERIES:[fieldQuestion,"Sí - Baterías"],DAMAGE_RESTRUCTURING:[fieldQuestion,"Sí - Reestructuración física / Sheetrock"],DAMAGE_LIGHTING:[fieldQuestion,"Sí - Iluminación"],DAMAGE_METALWORK:[fieldQuestion,"Sí - Herrería"],DAMAGE_GENERATOR_REQUEST:[fieldQuestion,"Sí - Requisición planta"],DAMAGE_ELECTRICAL:[fieldQuestion,"Sí - Avería eléctrica"],DAMAGE_SHUTTER:[fieldQuestion,"Sí - Falla en el shutter"],DAMAGE_GENERAL_OTHER:[fieldQuestion,"Sí - Otro servicio general"],DAMAGE_REPORTED:[fieldQuestion,"Sí - Tipo de avería no clasificado en el formulario histórico"]
  };
  function inferKey(row){
    var key=String(row&&row.findingKey||"").toUpperCase();if(origins[key])return key;
    var probe=normalize(String(row&&row.title||"")+" "+String(row&&row.detail||""));
    if(probe.includes("conectividad"))return"DAMAGE_CONNECTIVITY";if(probe.includes("cableado"))return"DAMAGE_WIRING";if(probe.includes("pantalla")||probe.includes("monitor"))return"DAMAGE_SCREENS";
    if(probe.includes("sticker de raza"))return"NO_RAZA_STICKER";if(probe.includes("sticker de real"))return"NO_REAL_STICKER";if(probe.includes("loteka"))return"LOTEKA_NOT_REMOVED";if(probe.includes("impresora")&&probe.includes("mantenimiento"))return"PRINTER_NO_MAINTENANCE";
    if(probe.includes("agencia no pintada")||probe.includes("no cumple con la pintura"))return"NOT_PAINTED";if(probe.includes("agencia sin inversor")||probe.includes("no tiene inversor"))return"NO_INVERTER";if(probe.includes("agencia sin bateria")||probe.includes("no tiene bateria"))return"NO_BATTERY";
    if(probe.includes("sheetrock")||probe.includes("reestructuracion"))return"DAMAGE_RESTRUCTURING";if(probe.includes("iluminacion"))return"DAMAGE_LIGHTING";if(probe.includes("herreria"))return"DAMAGE_METALWORK";if(probe.includes("planta electrica")||probe.includes("requisicion planta"))return"DAMAGE_GENERATOR_REQUEST";if(probe.includes("electrica"))return"DAMAGE_ELECTRICAL";if(probe.includes("shutter"))return"DAMAGE_SHUTTER";if(probe.includes("pintura"))return"DAMAGE_PAINTING";if(probe.includes("inversor"))return"DAMAGE_INVERTER";if(probe.includes("bateria"))return"DAMAGE_BATTERIES";
    return key;
  }
  function enrich(row){
    var source=Object.assign({},row||{}),key=inferKey(source),origin=origins[key];if(!origin)return source;
    source.findingKey=key;source.sourceQuestion=text(source.sourceQuestion,origin[0]);source.sourceAnswer=text(source.sourceAnswer,origin[1]);source.triggerReason=text(source.triggerReason,key.indexOf("DAMAGE_")===0?"En Auditoría de campo se respondió “Sí” y se seleccionó “"+origin[1].replace(/^Sí - /,"")+"”.":"En Auditoría de campo se respondió “No”; esa respuesta activó la creación automática del hallazgo.");return source;
  }

  function createDocument(rows,period,logoData){
    if(!global.jspdf||!global.jspdf.jsPDF)throw new Error("El motor PDF no está disponible.");
    rows=(Array.isArray(rows)?rows:[]).map(enrich);
    var jsPDF=global.jspdf.jsPDF;
    var pdf=new jsPDF({orientation:"portrait",unit:"mm",format:"a4",compress:true,putOnlyUsedFonts:true});
    var width=210,height=297,margin=16,contentWidth=178;
    var navy=[4,22,38],navyCard=[9,42,69],cyan=[27,190,220],ink=[20,43,58],muted=[88,112,126],line=[205,222,231],pale=[241,248,251];

    function header(title,subtitle){
      pdf.setFillColor.apply(pdf,navy);pdf.rect(0,0,width,39,"F");
      pdf.setFillColor.apply(pdf,cyan);pdf.rect(0,39,width,1.8,"F");
      pdf.setTextColor(255,255,255);pdf.setFont("helvetica","bold");pdf.setFontSize(16);pdf.text(title,margin,18,{maxWidth:132});
      pdf.setTextColor(190,229,241);pdf.setFont("helvetica","normal");pdf.setFontSize(8.5);pdf.text(subtitle,margin,29,{maxWidth:132});
      if(logoData)pdf.addImage(logoData,"PNG",162,7,31,14,undefined,"FAST");
      else{pdf.setTextColor(255,220,10);pdf.setFont("helvetica","bold");pdf.setFontSize(15);pdf.text("REAL",193,17,{align:"right"});}
      pdf.setTextColor(177,207,221);pdf.setFont("helvetica","bold");pdf.setFontSize(6.5);pdf.text("GRUPO TEJEDA - OFICINA VIRTUAL",193,28,{align:"right"});
    }

    function footer(page,total){
      pdf.setDrawColor.apply(pdf,line);pdf.line(margin,284,width-margin,284);
      pdf.setTextColor.apply(pdf,muted);pdf.setFont("helvetica","normal");pdf.setFontSize(7.5);
      pdf.text("Grupo Tejeda - Oficina Virtual - Reporte ejecutivo institucional",margin,290);
      pdf.text(String(page)+" / "+String(total),width-margin,290,{align:"right"});
    }

    function section(label,value,y){
      var pending=pdf.splitTextToSize(text(value),contentWidth-10),continued=false;
      while(pending.length){
        var available=Math.floor((278-y-12)/4.3);
        if(available<2){pdf.addPage();header("Continuación del hallazgo","Detalle y trazabilidad");y=48;available=Math.floor((278-y-12)/4.3);}
        var current=pending.splice(0,available),blockHeight=Math.max(17,10+current.length*4.3);
        pdf.setFillColor.apply(pdf,pale);pdf.setDrawColor.apply(pdf,line);pdf.roundedRect(margin,y,contentWidth,blockHeight,2.4,2.4,"FD");
        pdf.setTextColor(0,111,145);pdf.setFont("helvetica","bold");pdf.setFontSize(7.4);pdf.text((label+(continued?" (continuación)":"")).toUpperCase(),margin+5,y+6);
        pdf.setTextColor.apply(pdf,ink);pdf.setFont("helvetica","normal");pdf.setFontSize(9);pdf.text(current,margin+5,y+12,{lineHeightFactor:1.2});
        y+=blockHeight+3;continued=true;
        if(pending.length){pdf.addPage();header("Continuación del hallazgo","Detalle y trazabilidad");y=48;}
      }
      return y;
    }

    header("Hallazgos automáticos","Detalle ejecutivo completo");
    pdf.setFillColor.apply(pdf,navyCard);pdf.roundedRect(margin,55,contentWidth,42,4,4,"F");
    pdf.setTextColor.apply(pdf,cyan);pdf.setFont("helvetica","bold");pdf.setFontSize(28);pdf.text(String(rows.length),margin+8,76);
    pdf.setTextColor(255,255,255);pdf.setFontSize(10);pdf.text("Hallazgos incluidos",margin+8,87);
    pdf.setTextColor.apply(pdf,muted);pdf.setFont("helvetica","normal");pdf.setFontSize(9);pdf.text(text(period,"Período no especificado."),margin,112,{maxWidth:contentWidth});
    pdf.setTextColor.apply(pdf,ink);pdf.setFontSize(10);pdf.text(pdf.splitTextToSize("Este documento identifica la pregunta que originó cada hallazgo, explica la regla que lo creó y presenta su diagnóstico, solución, verificación y trazabilidad.",contentWidth),margin,130,{lineHeightFactor:1.35});

    rows.forEach(function(row,index){
      pdf.addPage();
      header("Hallazgo "+String(index+1)+" de "+String(rows.length),text(row.agencyCode,"Sin código")+" - "+text(row.agency,"Agencia sin identificar")+" - "+text(row.group,"Sin grupo"));
      var y=49;
      pdf.setTextColor.apply(pdf,ink);pdf.setFont("helvetica","bold");pdf.setFontSize(14);
      var titleLines=pdf.splitTextToSize(text(row.title,"Hallazgo automático"),contentWidth);pdf.text(titleLines,margin,y,{lineHeightFactor:1.15});y+=titleLines.length*6+4;
      y=section("Pregunta de auditoría que originó el hallazgo",row.sourceQuestion,y);
      y=section("Respuesta registrada en Auditoría de campo",row.sourceAnswer,y);
      y=section("Por qué se creó automáticamente",row.triggerReason,y);
      y=section("Hallazgo detectado",row.detail,y);
      y=section("Diagnóstico técnico",row.diagnosis,y);
      y=section("Solución aplicada",row.solution,y);
      y=section("Verificación final",row.verification,y);
      y=section("Recomendaciones",row.recommendations,y);
      y=section("Responsables y trazabilidad","Supervisor: "+text(row.supervisor,"No registrado")+" | Técnico: "+text(row.assignedTechnician,"Sin asignar")+" | Resuelto por: "+text(row.resolvedBy,"No registrado")+" | Evidencias: "+String(Number(row.evidenceCount||0)),y);
    });

    var total=pdf.getNumberOfPages();
    for(var page=1;page<=total;page++){pdf.setPage(page);footer(page,total);}
    return pdf;
  }

  function loadLogo(){
    if(logoPromise)return logoPromise;
    logoPromise=global.fetch("/loto-real-logo-transparent.png",{cache:"force-cache"}).then(function(response){if(!response.ok)throw new Error("No se pudo cargar el logo REAL.");return response.blob();}).then(function(blob){return new Promise(function(resolve,reject){var reader=new FileReader();reader.onload=function(){resolve(String(reader.result));};reader.onerror=reject;reader.readAsDataURL(blob);});}).catch(function(){logoPromise=null;return null;});
    return logoPromise;
  }

  async function download(rows,period){
    var safeRows=Array.isArray(rows)?rows:[];
    var logo=await loadLogo();
    var pdf=createDocument(safeRows,period,logo);
    pdf.save("Hallazgos-automaticos-detalle-"+new Date().toISOString().slice(0,10)+".pdf");
  }

  global.V520FindingsPdf={build:createDocument,download:download,enrich:enrich};
})(typeof window!=="undefined"?window:globalThis);
