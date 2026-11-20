import {useEffect,useRef,useState} from "react";
import {Bot,CheckCircle2,Mic,MicOff,Sparkles} from "lucide-react";
import "./voiceOperationAssistant.css";

type RecognitionEvent={results:ArrayLike<{0:{transcript:string};isFinal:boolean}>};
type RecognitionInstance={lang:string;continuous:boolean;interimResults:boolean;start:()=>void;stop:()=>void;onresult:((event:RecognitionEvent)=>void)|null;onerror:((event:{error:string})=>void)|null;onend:(()=>void)|null};
type RecognitionConstructor=new()=>RecognitionInstance;

export default function VoiceOperationAssistant({kind,onApply,disabled=false}:{kind:"ticket"|"warehouse";onApply:(transcript:string)=>string|void;disabled?:boolean}){
  const [open,setOpen]=useState(false),[listening,setListening]=useState(false),[transcript,setTranscript]=useState(""),[message,setMessage]=useState("");
  const recognition=useRef<RecognitionInstance|null>(null);
  useEffect(()=>()=>recognition.current?.stop(),[]);
  const start=()=>{
    const BrowserRecognition=(window as unknown as {SpeechRecognition?:RecognitionConstructor;webkitSpeechRecognition?:RecognitionConstructor}).SpeechRecognition||(window as unknown as {webkitSpeechRecognition?:RecognitionConstructor}).webkitSpeechRecognition;
    if(!BrowserRecognition){setMessage("Este navegador no permite dictado. Escribe la instrucción en el cuadro y la IA la organizará.");return;}
    const instance=new BrowserRecognition();recognition.current=instance;instance.lang="es-DO";instance.continuous=true;instance.interimResults=true;
    instance.onresult=event=>{let value="";for(let index=0;index<event.results.length;index++)value+=`${event.results[index][0].transcript} `;setTranscript(value.trim());};
    instance.onerror=event=>{setListening(false);setMessage(event.error==="not-allowed"?"Permite el micrófono en el navegador para usar la voz.":"No pude escuchar con claridad. Puedes intentarlo otra vez o escribir la instrucción.");};
    instance.onend=()=>setListening(false);setMessage("");setListening(true);instance.start();
  };
  const stop=()=>{recognition.current?.stop();setListening(false)};
  const apply=()=>{if(!transcript.trim()){setMessage("Dicta o escribe primero la solicitud.");return;}const result=onApply(transcript.trim());setMessage(result||"Datos preparados. Revisa el resumen y pulsa Enviar para crear el registro.");};
  return <section className={`voice-operation-assistant${open?" is-open":""}`} aria-label={`Asistente de voz para ${kind==="ticket"?"tickets":"solicitudes"}`}>
    <button type="button" className="voice-assistant-toggle" disabled={disabled} onClick={()=>setOpen(value=>!value)}><Bot/><span><strong>IA por voz</strong><small>{kind==="ticket"?"Crear ticket hablando":"Crear solicitud hablando"}</small></span><Sparkles/></button>
    {open&&<div className="voice-assistant-panel"><header><div><strong>Cuéntame qué necesitas</strong><small>Habla natural; la IA organizará los datos antes de crear.</small></div><button type="button" className={listening?"is-listening":""} onClick={listening?stop:start}>{listening?<MicOff/>:<Mic/>}{listening?"Detener":"Hablar"}</button></header>
      <textarea disabled={disabled} value={transcript} onChange={event=>setTranscript(event.target.value)} placeholder={kind==="ticket"?"Ej.: Crea un ticket de internet para la agencia 6011055; el módem se desconecta cada cinco minutos.":"Ej.: Solicita 2 módems y 3 cables de red para la agencia 6011055, por reemplazo de equipos dañados."}/>
      {message&&<p role="status">{message}</p>}
      <button type="button" className="voice-assistant-apply" disabled={disabled||!transcript.trim()} onClick={apply}><CheckCircle2/>Preparar y revisar</button>
    </div>}
  </section>;
}
