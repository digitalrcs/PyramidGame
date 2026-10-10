let token='';
export async function request(path,body) {
  const response=await fetch(`api/${path}`,{credentials:'same-origin',method:body===undefined?'GET':'POST',headers:body===undefined?{}:{'Content-Type':'application/json','X-Pyramid-CSRF':token},body:body===undefined?undefined:JSON.stringify(body)});
  const text=await response.text(); let data;
  try{data=text?JSON.parse(text):{};}catch{throw new Error('The server returned an unexpected response. Please try again.');}
  if(!response.ok){const error=new Error(data.error||(response.status===429?'Too many requests. Please wait a minute.':response.status===401?'Your session has ended. Please sign in again.':'The request failed. Please try again.'));error.status=response.status;error.state=data.state;error.play=data.play;throw error;}
  return data;
}
export async function session(){const result=await request('session');token=result.token;return result;}
