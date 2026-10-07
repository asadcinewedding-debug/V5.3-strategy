from pathlib import Path
import argparse, os, sys, math, traceback
import cv2
import numpy as np
import onnxruntime as ort
from PIL import Image, ImageCms

ROOT=Path(__file__).resolve().parent.parent
MODEL_DIR=ROOT/"models"
os.environ.setdefault("HF_HOME", str(Path(os.environ.get("LOCALAPPDATA", Path.home()))/"AN-AI-Relight-V5.3"/"models"/"hf"))
os.environ.setdefault("HF_HUB_DISABLE_TELEMETRY","1")

PROMPTS={
    "cinematic_warm":"cinematic warm portrait lighting, premium wedding photography, elegant amber highlights, soft dimensional shadows, realistic skin, luxurious atmosphere, natural photographic color",
    "luxury_indoor":"luxury indoor portrait lighting, warm practical ambience, polished editorial mood, soft directional key light, realistic skin and fabric, premium interior atmosphere",
    "golden_wedding":"golden hour wedding lighting, romantic warm glow, soft sunlit atmosphere, elegant highlights, realistic skin, premium cinematic wedding photography",
    "soft_romantic":"soft romantic portrait lighting, gentle luminous ambience, pastel warmth, delicate glow, natural skin, elegant wedding mood",
    "editorial_flash":"high-end editorial flash lighting, crisp dimensional subject light, controlled ambient background, luxury fashion photography, realistic skin and clothing",
    "moody_premium":"moody premium portrait lighting, deep elegant shadows, selective warm highlights, cinematic luxury atmosphere, realistic skin and fabric",
    "cool_blue_hour":"blue hour cinematic portrait lighting, refined cool ambience, subtle warm skin contrast, premium evening photography",
    "sunset_drama":"dramatic sunset lighting, warm orange side light, cinematic atmosphere, rich shadow depth, realistic skin and clothing",
    "filmic_wedding":"filmic wedding photography, cinematic color harmony, soft directional light, natural skin, rich but realistic atmosphere, premium analog-inspired mood",
    "dreamy_soft":"dreamy soft light, luminous wedding atmosphere, subtle bloom, gentle pastel warmth, realistic skin, elegant photographic mood"
}
NEGATIVE="different person, changed face, altered identity, changed hairstyle, changed pose, changed clothing, altered jewelry, deformed hands, extra fingers, warped anatomy, changed composition, lowres, blurry face, bad anatomy, bad hands, plastic skin, cartoon, illustration"

LIGHTS={"left":"left","right":"right","top":"top","bottom":"bottom","ambient":"ambient"}

def read_image(path):
    data=np.fromfile(str(path),dtype=np.uint8)
    im=cv2.imdecode(data,cv2.IMREAD_COLOR)
    if im is None:
        raise ValueError("Cannot decode input image")
    return im

def srgb_profile_bytes():
    try:
        return ImageCms.ImageCmsProfile(ImageCms.createProfile("sRGB")).tobytes()
    except Exception:
        return None

def write_image(path,im):
    p=Path(path); p.parent.mkdir(parents=True,exist_ok=True)
    rgb=cv2.cvtColor(im,cv2.COLOR_BGR2RGB)
    image=Image.fromarray(rgb)
    icc=srgb_profile_bytes()
    if p.suffix.lower() in (".tif",".tiff"):
        kw={"compression":"tiff_lzw"}
        if icc: kw["icc_profile"]=icc
        image.save(str(p),format="TIFF",**kw)
    else:
        kw={"quality":96,"subsampling":0}
        if icc: kw["icc_profile"]=icc
        image.save(str(p),format="JPEG",**kw)

def ort_session(path):
    so=ort.SessionOptions()
    so.intra_op_num_threads=max(1,min(4,(os.cpu_count() or 4)//2))
    so.inter_op_num_threads=1
    so.graph_optimization_level=ort.GraphOptimizationLevel.ORT_ENABLE_ALL
    return ort.InferenceSession(str(path),sess_options=so,providers=["CPUExecutionProvider"])

class MODNet:
    def __init__(self):
        self.session=ort_session(MODEL_DIR/"modnet_photographic.onnx")
        self.input=self.session.get_inputs()[0].name
        self.outputs=[x.name for x in self.session.get_outputs()]
    def predict(self,bgr):
        h,w=bgr.shape[:2]
        rgb=cv2.cvtColor(bgr,cv2.COLOR_BGR2RGB)
        target=512
        if w>=h:
            nh=target; nw=max(32,int(w/h*target))
        else:
            nw=target; nh=max(32,int(h/w*target))
        nh-=nh%32; nw-=nw%32
        x=cv2.resize(rgb,(nw,nh),interpolation=cv2.INTER_AREA).astype(np.float32)/255.0
        x=(x-.5)/.5
        x=np.transpose(x,(2,0,1))[None]
        matte=self.session.run(self.outputs,{self.input:x})[0]
        matte=np.squeeze(matte).astype(np.float32,copy=False)
        matte=cv2.resize(matte,(w,h),interpolation=cv2.INTER_CUBIC).astype(np.float32,copy=False)
        matte=np.ascontiguousarray(np.clip(matte,0,1),dtype=np.float32)
        return cv2.GaussianBlur(matte,(0,0),1.2).astype(np.float32,copy=False)

class FaceDetector:
    def __init__(self):
        self.detector=None
        try:
            self.detector=cv2.FaceDetectorYN_create(
                str(MODEL_DIR/"face_detection_yunet_2023mar.onnx"),
                "",(320,320),0.72,0.3,5000)
        except Exception:
            self.detector=None
    def mask(self,bgr):
        h,w=bgr.shape[:2]
        mask=np.zeros((h,w),dtype=np.float32)
        if self.detector is None:
            return mask
        scale=min(1.0,1200.0/max(h,w))
        small=cv2.resize(bgr,(max(1,int(w*scale)),max(1,int(h*scale)))) if scale<1 else bgr
        sh,sw=small.shape[:2]
        try:
            self.detector.setInputSize((sw,sh))
            _,faces=self.detector.detect(small)
        except Exception:
            return mask
        if faces is None:
            return mask
        yy,xx=np.mgrid[0:h,0:w].astype(np.float32)
        for f in faces:
            x,y,bw,bh=f[:4]/scale
            cx=x+bw*.5; cy=y+bh*.50
            rx=max(4,bw*.70); ry=max(4,bh*.86)
            e=1.0-(((xx-cx)/rx)**2+((yy-cy)/ry)**2)
            mask=np.maximum(mask,np.clip(e,0,1).astype(np.float32))
        return cv2.GaussianBlur(mask,(0,0),max(1.5,min(h,w)*.006)).astype(np.float32)

def fit_size(w,h,max_side):
    scale=float(max_side)/max(w,h)
    nw=max(256,int(round(w*scale/64.0))*64)
    nh=max(256,int(round(h*scale/64.0))*64)
    nw=min(nw,max_side if max_side%64==0 else int(max_side//64)*64)
    nh=min(nh,max_side if max_side%64==0 else int(max_side//64)*64)
    return max(256,nw),max(256,nh)

def resize_center_crop(image,target_width,target_height):
    pil=Image.fromarray(image)
    ow,oh=pil.size
    scale=max(target_width/ow,target_height/oh)
    rw=int(round(ow*scale)); rh=int(round(oh*scale))
    pil=pil.resize((rw,rh),Image.LANCZOS)
    l=(rw-target_width)//2; t=(rh-target_height)//2
    return np.array(pil.crop((l,t,l+target_width,t+target_height)))

def background_gradient(width,height,source):
    if source=="left":
        g=np.linspace(255,0,width,dtype=np.float32)[None,:]
        a=np.repeat(g,height,axis=0)
    elif source=="right":
        g=np.linspace(0,255,width,dtype=np.float32)[None,:]
        a=np.repeat(g,height,axis=0)
    elif source=="top":
        g=np.linspace(255,0,height,dtype=np.float32)[:,None]
        a=np.repeat(g,width,axis=1)
    elif source=="bottom":
        g=np.linspace(0,255,height,dtype=np.float32)[:,None]
        a=np.repeat(g,width,axis=1)
    else:
        y=np.linspace(0,1,height,dtype=np.float32)[:,None]
        x=np.linspace(0,1,width,dtype=np.float32)[None,:]
        a=150+22*np.cos((x-.5)*math.pi)+12*np.cos((y-.45)*math.pi)
        a=np.clip(a,0,255)
    return np.stack([a,a,a],axis=-1).astype(np.uint8)

def protected_foreground(bgr,alpha,width,height):
    rgb=cv2.cvtColor(bgr,cv2.COLOR_BGR2RGB)
    rgb=resize_center_crop(rgb,width,height).astype(np.float32)
    a=resize_center_crop((np.clip(alpha,0,1)*255).astype(np.uint8),width,height).astype(np.float32)/255.0
    if a.ndim==3: a=a[...,0]
    fg=127.0+(rgb-127.0)*a[...,None]
    return np.clip(fg,0,255).astype(np.uint8)

class LocalICLight:
    def __init__(self,device_mode="auto"):
        import torch
        import torch.nn as nn
        import safetensors.torch as sf
        from huggingface_hub import hf_hub_download
        from transformers import CLIPTextModel, CLIPTokenizer
        from diffusers import AutoencoderKL, UNet2DConditionModel, StableDiffusionImg2ImgPipeline, DDIMScheduler

        self.torch=torch
        self.device_name="cpu"
        device=torch.device("cpu")
        if device_mode in ("auto","directml"):
            try:
                import torch_directml
                device=torch_directml.device()
                test=torch.tensor([1.0]).to(device)
                _=(test+1.0).cpu()
                self.device_name="DirectML"
            except Exception as e:
                if device_mode=="directml":
                    raise RuntimeError("DirectML GPU initialization failed: "+str(e))
                device=torch.device("cpu")
                self.device_name="CPU fallback"
        else:
            self.device_name="CPU"

        self.device=device
        self.dtype=torch.float32
        base="stablediffusionapi/realistic-vision-v51"

        tokenizer=CLIPTokenizer.from_pretrained(base,subfolder="tokenizer")
        text_encoder=CLIPTextModel.from_pretrained(base,subfolder="text_encoder",torch_dtype=self.dtype)
        vae=AutoencoderKL.from_pretrained(base,subfolder="vae",torch_dtype=self.dtype)
        unet=UNet2DConditionModel.from_pretrained(base,subfolder="unet",torch_dtype=self.dtype)

        with torch.no_grad():
            old=unet.conv_in
            new=nn.Conv2d(8,old.out_channels,old.kernel_size,old.stride,old.padding)
            new.weight.zero_()
            new.weight[:,:4,:,:].copy_(old.weight)
            new.bias=old.bias
            unet.conv_in=new

        offset=hf_hub_download(repo_id="lllyasviel/ic-light",filename="iclight_sd15_fc.safetensors")
        sd_offset=sf.load_file(offset)
        origin=unet.state_dict()
        merged={k:origin[k]+sd_offset[k] for k in origin.keys()}
        unet.load_state_dict(merged,strict=True)
        del sd_offset,origin,merged

        original_forward=unet.forward
        def hooked(sample,timestep,encoder_hidden_states,**kwargs):
            ca=kwargs.get("cross_attention_kwargs") or {}
            concat=ca.get("concat_conds")
            if concat is None:
                raise RuntimeError("IC-Light concat condition missing")
            concat=concat.to(sample)
            concat=torch.cat([concat]*(sample.shape[0]//concat.shape[0]),dim=0)
            kwargs["cross_attention_kwargs"]={}
            return original_forward(torch.cat([sample,concat],dim=1),timestep,encoder_hidden_states,**kwargs)
        unet.forward=hooked

        scheduler=DDIMScheduler(
            num_train_timesteps=1000,
            beta_start=0.00085,
            beta_end=0.012,
            beta_schedule="scaled_linear",
            clip_sample=False,
            set_alpha_to_one=False,
            steps_offset=1)

        text_encoder=text_encoder.to(device=device,dtype=self.dtype)
        vae=vae.to(device=device,dtype=self.dtype)
        unet=unet.to(device=device,dtype=self.dtype)

        pipe=StableDiffusionImg2ImgPipeline(
            vae=vae,text_encoder=text_encoder,tokenizer=tokenizer,unet=unet,
            scheduler=scheduler,safety_checker=None,requires_safety_checker=False,
            feature_extractor=None,image_encoder=None)
        self.pipe=pipe
        self.vae=vae

    def encode_concat(self,fg_np):
        torch=self.torch
        x=torch.from_numpy(fg_np.astype(np.float32)/127.5-1.0).permute(2,0,1)[None]
        x=x.to(device=self.device,dtype=self.dtype)
        with torch.no_grad():
            lat=self.vae.encode(x).latent_dist.mode()*self.vae.config.scaling_factor
        return lat

    def generate(self,bgr,alpha,cfg):
        rgb=cv2.cvtColor(bgr,cv2.COLOR_BGR2RGB)
        h,w=bgr.shape[:2]
        gw,gh=fit_size(w,h,int(cfg["modelResolution"]))
        fg=protected_foreground(bgr,alpha,gw,gh)
        concat=self.encode_concat(fg)

        bg=background_gradient(gw,gh,cfg["lightSource"])
        bg_pil=Image.fromarray(bg)

        mood=np.clip(cfg["moodStrength"]/100.0,0,1)
        prompt_inf=np.clip(cfg["promptInfluence"]/100.0,0,1)
        if cfg["mode"]=="safe":
            strength=.44+.10*mood; steps=16; guidance=1.7+.4*prompt_inf
        elif cfg["mode"]=="strong":
            strength=.68+.18*mood; steps=24; guidance=2.2+.7*prompt_inf
        else:
            strength=.56+.14*mood; steps=20; guidance=1.9+.5*prompt_inf

        prompt=PROMPTS[cfg["preset"]]+", coherent scene-wide illumination, preserve composition, photorealistic"
        if cfg["lightSource"]=="ambient":
            prompt += ", balanced environmental light"

        with self.torch.no_grad():
            result=self.pipe(
                prompt=prompt,
                negative_prompt=NEGATIVE,
                image=bg_pil,
                strength=float(strength),
                num_inference_steps=int(max(8,round(steps/max(strength,.1)))),
                guidance_scale=float(guidance),
                cross_attention_kwargs={"concat_conds":concat},
                output_type="np").images[0]

        out=np.clip(result*255.0,0,255).astype(np.uint8)
        return cv2.cvtColor(out,cv2.COLOR_RGB2BGR)

def local_tone_map(orig,gen,alpha,face,cfg):
    h,w=orig.shape[:2]
    gen=cv2.resize(gen,(w,h),interpolation=cv2.INTER_CUBIC)
    o=orig.astype(np.float32)/255.0
    g=gen.astype(np.float32)/255.0

    sigma=max(12.0,min(h,w)*.032)
    o_low=cv2.GaussianBlur(o,(0,0),sigma).astype(np.float32)
    g_low=cv2.GaussianBlur(g,(0,0),sigma).astype(np.float32)

    ratio=np.clip((g_low+.04)/(o_low+.04),.52,1.90).astype(np.float32)
    delta=np.clip(g_low-o_low,-.36,.36).astype(np.float32)

    mood=np.clip(cfg["moodStrength"]/100.0,0,1)
    original=np.clip(cfg["originalLight"]/100.0,0,1)
    strength=mood*(1.0-original*.72)
    if cfg["mode"]=="safe": strength*=.68
    elif cfg["mode"]=="strong": strength*=1.12

    relit=np.clip(o*(1+(ratio-1)*strength)+delta*(.34*strength),0,1)

    structure=np.clip(cfg["structurePreservation"]/100.0,0,1)
    relit=o*(structure*.30)+relit*(1-structure*.30)

    subj=np.clip(alpha,0,1)[...,None]
    identity=np.clip(cfg["identityProtection"]/100.0,0,1)
    subject_keep=identity*.48
    relit=relit*(1-subj*subject_keep)+o*(subj*subject_keep)

    fm=np.clip(face,0,1)[...,None]*(np.clip(cfg["faceProtection"]/100.0,0,1)*.72)
    relit=relit*(1-fm)+o*fm

    detail=o-o_low
    relit_low=cv2.GaussianBlur(relit.astype(np.float32),(0,0),sigma)
    out=np.clip(relit_low+detail*(.88+.10*structure),0,1)

    lum=cv2.cvtColor((out*255).astype(np.uint8),cv2.COLOR_BGR2GRAY).astype(np.float32)/255.0
    hp=np.clip((lum-.90)/.10,0,1)[...,None]
    out=out*(1-.42*hp)+o*(.42*hp)

    return np.clip(out*255,0,255).astype(np.uint8)

def parse_row(parts):
    if len(parts)!=15:
        raise ValueError(f"Expected 15 manifest columns, got {len(parts)}")
    sid,inp,tif,jpg,mode,preset,light,mood,identity,structure,face,original,prompt,device,res=parts
    return sid,inp,tif,jpg,{
        "mode":mode,"preset":preset,"lightSource":light,
        "moodStrength":float(mood),"identityProtection":float(identity),
        "structurePreservation":float(structure),"faceProtection":float(face),
        "originalLight":float(original),"promptInfluence":float(prompt),
        "deviceMode":device,"modelResolution":int(float(res))
    }

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--manifest",required=True)
    ap.add_argument("--results",required=True)
    a=ap.parse_args()

    lines=Path(a.manifest).read_text(encoding="utf-8").splitlines()
    if not lines or lines[0]!="ANAI_RELIGHT_V53_BATCH_1":
        raise ValueError("Unsupported V5.3 manifest")

    matte=MODNet()
    face_detector=FaceDetector()
    engine=None
    engine_device=None
    results=["ANAI_RELIGHT_V53_RESULTS_1"]
    hard_fail=False

    for line in lines[1:]:
        if not line.strip(): continue
        sid="0"
        try:
            sid,inp,tif,jpg,cfg=parse_row(line.split("\t"))
            if engine is None or engine_device!=cfg["deviceMode"]:
                engine=LocalICLight(cfg["deviceMode"])
                engine_device=cfg["deviceMode"]

            orig=read_image(inp)
            alpha=matte.predict(orig)
            face=face_detector.mask(orig)
            gen=engine.generate(orig,alpha,cfg)
            out=local_tone_map(orig,gen,alpha,face,cfg)

            write_image(tif,out)
            if jpg: write_image(jpg,out)
            results.append(f"{sid}\tok\tLocal IC-Light via {engine.device_name}; original subject detail protected")
        except Exception as e:
            hard_fail=True
            msg=(str(e)+" | "+traceback.format_exc(limit=2)).replace("\t"," ").replace("\r"," ").replace("\n"," ")[:1000]
            results.append(f"{sid}\terror\t{msg}")

    Path(a.results).write_text("\n".join(results)+"\n",encoding="utf-8")
    raise SystemExit(2 if hard_fail else 0)

if __name__=="__main__":
    main()
