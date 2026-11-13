const terminal = new Terminal({allowTransparency:true});
const fit = new FitAddon.FitAddon();
terminal.loadAddon(fit);
terminal.open(document.getElementById('terminal'));
let ready = false, configured = false;
const send = value => window.chrome.webview.postMessage(value);
function resize() { if(configured) { fit.fit(); send({type:'size',cols:terminal.cols,rows:terminal.rows}); } }
terminal.onData(data => { if(ready) send({type:'input',data}); });
window.chrome.webview.addEventListener('message', async event => {
  const message = event.data;
  if(message.type === 'configure') {
    const options = message.options;
    document.body.style.background = options.theme.background || '#0c0c0c';
    document.getElementById('terminal').style.background='transparent';
    if(message.backgroundImage) {
      const background = document.createElement('div');
      Object.assign(background.style, {position:'absolute',inset:'0',pointerEvents:'none',backgroundImage:`url("${message.backgroundImage}")`,backgroundPosition:'center',backgroundRepeat:'no-repeat',backgroundSize:message.backgroundSize,opacity:String(message.backgroundOpacity)});
      document.body.prepend(background);
      document.getElementById('terminal').style.position='relative';
      options.theme.background='#00000000';
    }
    Object.assign(terminal.options, options);
    const parts = message.padding.split(',').map(p => Math.max(0,Math.min(100,parseFloat(p)||0)));
    document.getElementById('terminal').style.padding=parts.map(p=>`${p}px`).join(' ');
    await document.fonts.load(`${terminal.options.fontSize}px ${terminal.options.fontFamily}`);
    configured=true; resize();
  }
  if(message.type === 'output') terminal.write(message.data);
  if(message.type === 'ready') { ready=true; resize(); terminal.focus(); }
});
new ResizeObserver(resize).observe(document.getElementById('terminal'));
send({type:'configure'});
