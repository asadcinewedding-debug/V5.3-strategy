local Binding=import 'LrBinding'
local Dialogs=import 'LrDialogs'
local Export=import 'LrExportSession'
local Files=import 'LrFileUtils'
local Path=import 'LrPathUtils'
local Progress=import 'LrProgressScope'
local Tasks=import 'LrTasks'
local UUID=import 'LrUUID'
local View=import 'LrView'
local Engine=require 'Engine'

local M={}

local PRESETS={
    cinematic_warm=true,luxury_indoor=true,golden_wedding=true,
    soft_romantic=true,editorial_flash=true,moody_premium=true,
    cool_blue_hour=true,sunset_drama=true,filmic_wedding=true,
    dreamy_soft=true
}

local LIGHTS={left=true,right=true,top=true,bottom=true,ambient=true}
local DEVICES={auto=true,directml=true,cpu=true}
local RESOLUTIONS={['512']=true,['640']=true,['768']=true}

local function number(v,lo,hi,label)
    local n=tonumber(v)
    if not n or n~=n or n<lo or n>hi then
        error(label..' must be between '..lo..' and '..hi)
    end
    return n
end

function M.configure(ctx,count,prefs)
    local p=Binding.makePropertyTable(ctx)
    local f=View.osFactory()

    p.mode='balanced'
    p.preset='cinematic_warm'
    p.lightSource='left'
    p.moodStrength=65
    p.identityProtection=95
    p.structurePreservation=94
    p.faceProtection=96
    p.originalLight=18
    p.promptInfluence=58
    p.deviceMode='auto'
    p.modelResolution='512'
    p.stack=true
    p.jpeg=false
    p.quality='high'

    local content=f:column{
        bind_to_object=p,
        spacing=f:control_spacing(),

        f:static_text{title='AN AI RELIGHT V5.3 — LOCAL GENERATIVE MOOD',font='<system/bold>',width_in_chars=88},
        f:static_text{title='No API • No cloud inference • Radeon/DirectML local generation • Subject unchanged',width_in_chars=88},

        f:group_box{
            title='1  LOCAL AI MODE',
            f:column{
                spacing=f:control_spacing(),
                f:row{
                    f:static_text{title='Mode',width_in_chars=25},
                    f:popup_menu{
                        value=View.bind('mode'),width_in_chars=36,
                        items={
                            {title='Safe Mood — maximum preservation',value='safe'},
                            {title='Balanced Creative — recommended',value='balanced'},
                            {title='Strong Mood — stronger atmosphere',value='strong'},
                        }
                    }
                },
                f:row{
                    f:static_text{title='Mood Preset',width_in_chars=25},
                    f:popup_menu{
                        value=View.bind('preset'),width_in_chars=36,
                        items={
                            {title='Cinematic Warm',value='cinematic_warm'},
                            {title='Luxury Indoor',value='luxury_indoor'},
                            {title='Golden Wedding',value='golden_wedding'},
                            {title='Soft Romantic',value='soft_romantic'},
                            {title='Editorial Flash',value='editorial_flash'},
                            {title='Moody Premium',value='moody_premium'},
                            {title='Cool Blue Hour',value='cool_blue_hour'},
                            {title='Sunset Drama',value='sunset_drama'},
                            {title='Filmic Wedding',value='filmic_wedding'},
                            {title='Dreamy Soft Light',value='dreamy_soft'},
                        }
                    }
                },
                f:row{
                    f:static_text{title='Virtual Light Direction',width_in_chars=25},
                    f:popup_menu{
                        value=View.bind('lightSource'),width_in_chars=28,
                        items={
                            {title='Left Light',value='left'},
                            {title='Right Light',value='right'},
                            {title='Top Light',value='top'},
                            {title='Bottom Light',value='bottom'},
                            {title='Ambient / Overall',value='ambient'},
                        }
                    }
                }
            }
        },

        f:group_box{
            title='2  LOCAL GPU / MODEL',
            f:column{
                spacing=f:control_spacing(),
                f:row{
                    f:static_text{title='Compute Device',width_in_chars=25},
                    f:popup_menu{
                        value=View.bind('deviceMode'),width_in_chars=34,
                        items={
                            {title='Auto — DirectML first, CPU fallback',value='auto'},
                            {title='DirectML GPU — AMD Radeon',value='directml'},
                            {title='CPU only — very slow',value='cpu'},
                        }
                    }
                },
                f:row{
                    f:static_text{title='AI Mood Resolution',width_in_chars=25},
                    f:popup_menu{
                        value=View.bind('modelResolution'),width_in_chars=34,
                        items={
                            {title='512 px — recommended for Radeon VII',value='512'},
                            {title='640 px — stronger detail / slower',value='640'},
                            {title='768 px — experimental / high VRAM',value='768'},
                        }
                    }
                },
                f:static_text{
                    title='First local run installs the Python/DirectML runtime and downloads the local IC-Light + SD model cache. This can take several GB and several minutes. Later runs are local/offline.',
                    width_in_chars=82,height_in_lines=3
                }
            }
        },

        f:group_box{
            title='3  AI MOOD CONTROLS',
            f:column{
                spacing=f:control_spacing(),
                f:row{
                    f:static_text{title='AI Mood Strength',width_in_chars=25},
                    f:edit_field{value=View.bind('moodStrength'),width_in_chars=8},
                    f:static_text{title='0–100'}
                },
                f:row{
                    f:static_text{title='Prompt Influence',width_in_chars=25},
                    f:edit_field{value=View.bind('promptInfluence'),width_in_chars=8},
                    f:static_text{title='0–100'}
                },
                f:row{
                    f:static_text{title='Original Light Retained',width_in_chars=25},
                    f:edit_field{value=View.bind('originalLight'),width_in_chars=8},
                    f:static_text{title='0–100'}
                },
            }
        },

        f:group_box{
            title='4  SUBJECT LOCK — ORIGINAL SUBJECT IS THE SOURCE OF DETAIL',
            f:column{
                spacing=f:control_spacing(),
                f:row{
                    f:static_text{title='Identity Protection',width_in_chars=25},
                    f:edit_field{value=View.bind('identityProtection'),width_in_chars=8},
                    f:static_text{title='Recommended 90–100'}
                },
                f:row{
                    f:static_text{title='Structure Preservation',width_in_chars=25},
                    f:edit_field{value=View.bind('structurePreservation'),width_in_chars=8},
                    f:static_text{title='Recommended 90–100'}
                },
                f:row{
                    f:static_text{title='Face Protection',width_in_chars=25},
                    f:edit_field{value=View.bind('faceProtection'),width_in_chars=8},
                    f:static_text{title='Recommended 90–100'}
                },
                f:static_text{
                    title='The generated frame is used only as a low-frequency illumination/color reference. Face, pose, clothing, body, composition and high-frequency texture come from the original photo.',
                    width_in_chars=82,height_in_lines=3
                }
            }
        },

        f:group_box{
            title='5  OUTPUT',
            f:column{
                spacing=f:control_spacing(),
                f:row{
                    f:static_text{title='Lightroom Source Quality',width_in_chars=25},
                    f:popup_menu{
                        value=View.bind('quality'),width_in_chars=32,
                        items={
                            {title='High — 4096 px transfer source',value='high'},
                            {title='Standard — 2560 px transfer source',value='standard'}
                        }
                    }
                },
                f:checkbox{title='Import and stack relit TIFF above original',value=View.bind('stack')},
                f:checkbox{title='Also save JPEG preview',value=View.bind('jpeg')},
                f:static_text{title=count..' selected still photo(s). Originals are never overwritten.',width_in_chars=78}
            }
        }
    }

    if Dialogs.presentModalDialog{
        title='AN AI Relight V5.3 — Local Generative Mood',
        contents=content,
        actionVerb='Run Local V5.3'
    }~='ok' then return nil end

    assert(p.mode=='safe' or p.mode=='balanced' or p.mode=='strong','Invalid V5.3 mode')
    assert(PRESETS[p.preset],'Invalid mood preset')
    assert(LIGHTS[p.lightSource],'Invalid light source')
    assert(DEVICES[p.deviceMode],'Invalid compute device')
    assert(RESOLUTIONS[p.modelResolution],'Invalid AI mood resolution')
    assert(p.quality=='high' or p.quality=='standard','Invalid quality')

    return {
        mode=p.mode,preset=p.preset,lightSource=p.lightSource,
        moodStrength=number(p.moodStrength,0,100,'AI mood strength'),
        identityProtection=number(p.identityProtection,0,100,'Identity protection'),
        structurePreservation=number(p.structurePreservation,0,100,'Structure preservation'),
        faceProtection=number(p.faceProtection,0,100,'Face protection'),
        originalLight=number(p.originalLight,0,100,'Original light retained'),
        promptInfluence=number(p.promptInfluence,0,100,'Prompt influence'),
        deviceMode=p.deviceMode,modelResolution=tonumber(p.modelResolution),
        stack=p.stack,jpeg=p.jpeg,quality=p.quality
    }
end

local function preflight()
    local exe=Path.child(_PLUGIN.path,Engine.executable())
    assert(Files.exists(exe),'AN AI Relight V5.3 local engine is missing. Run the V5.3 installer again.')
    return exe
end

local function safeStem(photo)
    local name=photo:getFormattedMetadata('fileName') or 'photo'
    local stem=name:gsub('%.[^%.]+$',''):gsub('[<>:"/\\|%?%*]','_')
    return stem~='' and stem or 'photo'
end

local function uniqueOutput(photo,suffix)
    local src=photo:getRawMetadata('path')
    assert(type(src)=='string' and src~='','Cannot determine source photo path.')
    local dir=Path.parent(src)
    local base=safeStem(photo)..'-ANAI-Relight-V5.3'
    local out=Path.child(dir,base..suffix)
    local n=2
    while Files.exists(out) do
        out=Path.child(dir,base..'-'..n..suffix)
        n=n+1
    end
    return out
end

local function renderSource(photo,dir,progress,quality)
    local maxEdge=quality=='high' and 4096 or 2560
    local session=Export{photosToExport={photo},exportSettings={
        LR_exportServiceProvider='com.adobe.ag.export.file',
        LR_export_destinationType='specificFolder',
        LR_export_destinationPathPrefix=dir,
        LR_export_useSubfolder=false,
        LR_collisionHandling='rename',
        LR_format='JPEG',
        LR_jpeg_quality=1,
        LR_export_colorSpace='sRGB',
        LR_size_doConstrain=true,
        LR_size_doNotEnlarge=true,
        LR_size_resizeType='longEdge',
        LR_size_maxWidth=maxEdge,
        LR_size_maxHeight=maxEdge,
        LR_size_units='pixels',
        LR_outputSharpeningOn=false,
        LR_useWatermark=false,
        LR_reimportExportedPhoto=false,
        LR_renamingTokensOn=false,
        LR_minimizeEmbeddedMetadata=true,
        LR_removeLocationMetadata=true
    }}
    local rendered
    for _,rendition in session:renditions{stopIfCanceled=true,progressScope=progress} do
        local ok,path=rendition:waitForRender()
        assert(ok,'V5.3 source render failed: '..tostring(path))
        rendered=path
    end
    assert(rendered,'Lightroom returned no V5.3 source render.')
    return rendered
end

function M.run(ctx,catalog,photos,skipped,config,prefs,write)
    local engineExe=preflight()
    local root=Path.child(Path.getStandardFilePath('temp'),'ANAI-Relight-V53-'..UUID.generateUUID())
    assert(Files.createAllDirectories(root),'Cannot create V5.3 temporary folder.')

    local progress=Progress{title='AN AI Relight V5.3 - Local Generative Mood',functionContext=ctx}
    progress:setCancelable(true)
    ctx:addCleanupHandler(function() progress:done();Files.delete(root) end)

    local manifestPath=Path.child(root,'manifest.tsv')
    local resultsPath=Path.child(root,'results.tsv')
    local logPath=Path.child(root,'engine.log')

    local mf=assert(io.open(manifestPath,'wb'))
    mf:write('ANAI_RELIGHT_V53_BATCH_1\n')

    local outputById,sourceById={},{}
    local prepared,prepFailed=0,{}

    for i,photo in ipairs(photos) do
        if progress:isCanceled() then break end
        progress:setCaption('Preparing '..i..' / '..#photos..' - '..(photo:getFormattedMetadata('fileName') or 'Photo'))
        local dir=Path.child(root,tostring(i))
        Files.createAllDirectories(dir)

        local ok,err=Tasks.pcall(function()
            local input=renderSource(photo,dir,progress,config.quality)
            local tif=uniqueOutput(photo,'.tif')
            local jpg=config.jpeg and uniqueOutput(photo,'.jpg') or ''
            outputById[i]=tif
            sourceById[i]=photo

            local row={
                i,input,tif,jpg,
                config.mode,config.preset,config.lightSource,
                config.moodStrength,config.identityProtection,
                config.structurePreservation,config.faceProtection,
                config.originalLight,config.promptInfluence,
                config.deviceMode,config.modelResolution
            }
            mf:write(table.concat(row,'\t')..'\n')
            prepared=prepared+1
        end)

        if not ok then
            prepFailed[#prepFailed+1]=(photo:getFormattedMetadata('fileName') or ('Photo '..i))..': '..tostring(err)
        end
        progress:setPortionComplete(i,#photos*2)
        Tasks.yield()
    end

    mf:close()
    if progress:isCanceled() then progress:done();return end
    assert(prepared>0,'No photos could be prepared for V5.3.')

    progress:setCaption('V5.3: local DirectML mood generation + protected full-resolution transfer...')
    local command=Engine.quote(engineExe)..
        ' --plugin-root '..Engine.quote(_PLUGIN.path)..
        ' --manifest '..Engine.quote(manifestPath)..
        ' --results '..Engine.quote(resultsPath)..
        ' > '..Engine.quote(logPath)..' 2>&1'

    local status=Tasks.execute(Engine.wrap(command))
    assert(Files.exists(resultsPath),'V5.3 local engine did not return results.\n'..(Files.readFile(logPath) or ''))

    local rows={}
    for line in (Files.readFile(resultsPath) or ''):gmatch('[^\r\n]+') do
        if line~='ANAI_RELIGHT_V53_RESULTS_1' then
            local id,state,message=line:match('^(%d+)\t(%a+)\t(.*)$')
            id=tonumber(id)
            if id and sourceById[id] then rows[id]={state=state,message=message} end
        end
    end

    local imported,failed=0,#prepFailed
    local report={
        'AN AI Relight V5.3 — Local Generative Mood',
        os.date('%Y-%m-%d %H:%M:%S'),
        'Mode: '..config.mode..' | Preset: '..config.preset..
        ' | Mood: '..config.moodStrength..
        ' | Device: '..config.deviceMode..
        ' | AI resolution: '..config.modelResolution
    }

    for _,s in ipairs(prepFailed) do report[#report+1]='PREP ERROR - '..s end

    for i,photo in ipairs(photos) do
        local row=rows[i]
        if row and row.state=='ok' and Files.exists(outputById[i]) then
            local ok,err=Tasks.pcall(function()
                write(catalog,'AN AI Relight V5.3 - Import',function()
                    if config.stack then
                        catalog:addPhoto(outputById[i],photo,'above')
                    else
                        catalog:addPhoto(outputById[i])
                    end
                end)
            end)
            if ok then
                imported=imported+1
                report[#report+1]=(photo:getFormattedMetadata('fileName') or ('Photo '..i))..': OK - '..row.message
            else
                failed=failed+1
                report[#report+1]=(photo:getFormattedMetadata('fileName') or ('Photo '..i))..': IMPORT ERROR - '..tostring(err)
            end
        elseif outputById[i] then
            failed=failed+1
            report[#report+1]=(photo:getFormattedMetadata('fileName') or ('Photo '..i))..': ERROR - '..(row and row.message or 'Missing local-engine result')
        end
        progress:setPortionComplete(#photos+i,#photos*2)
        Tasks.yield()
    end

    progress:done()
    report[#report+1]='Rendered/imported: '..imported
    report[#report+1]='Failed: '..failed
    report[#report+1]='Skipped videos: '..skipped
    prefs.lastV53RelightReport=table.concat(report,'\n')
    Dialogs.message('AN AI Relight V5.3',prefs.lastV53RelightReport,'info')
end

return M
