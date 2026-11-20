import {describe,expect,it} from "vitest";
import {warehouseProductMatches} from "../src/components/vite/WarehouseRequestComposer";
import {warehouseRequestMatches} from "../src/components/vite/WarehousePdfRequests";

const product={productId:"00200004",name:"Abanicos De Pared (Hw-16)",group:"Electrico",division:"Producto Terminado",warehouse:"PRINCIPAL"};

describe("buscador de productos en solicitudes a Almacén",()=>{
  it("busca por ID de producto",()=>expect(warehouseProductMatches(product,"00200004")).toBe(true));
  it("busca por nombre sin depender de mayúsculas ni acentos",()=>expect(warehouseProductMatches(product,"ABANICOS de pared")).toBe(true));
  it("descarta productos que no coinciden",()=>expect(warehouseProductMatches(product,"impresora térmica")).toBe(false));
});

describe("buscador de formularios de Almacén",()=>{
  const request={
    id:1,title:"Solicitud de equipo",description:"Cambio preventivo",originalFileName:"SOL-ALM-100.pdf",contentType:"application/pdf",size:100,
    ownerUserName:"user",ownerDisplayName:"José Pérez",department:"Tecnología",status:"received",createdUtc:"2026-10-29T00:00:00Z",isOwner:false,canEdit:false,sharedWith:[],
    warehouseRequest:{number:"SOL-ALM-100",agencyId:"a",agencyCode:"632084",agencyName:"Villa Mella",agencyGroup:"GRUPO SANTOS",installerId:"",installerName:"",priority:"MEDIUM",lines:[{kind:"EQUIPMENT",productId:"00200004",name:"Abanicos De Pared",quantity:2}]},
  };
  it.each(["00200004","abanicos","Jose Perez","632084","grupo santos","SOL-ALM-100"])("encuentra %s",query=>expect(warehouseRequestMatches(request,query)).toBe(true));
  it("descarta lo que no pertenece a la solicitud",()=>expect(warehouseRequestMatches(request,"impresora térmica")).toBe(false));
});
