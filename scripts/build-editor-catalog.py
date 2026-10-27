#!/usr/bin/env python3
"""Compile solver profiles + presets into the shared UI/game catalogue. --check detects drift."""
import argparse,copy,json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
def read(name): return json.loads((root/'data'/name).read_text())
profiles=read('vegetation-environment.v1.json')['profiles']
measured={a['id']:a for a in read('base-game-vegetation.v1.json')['assets']}
presets=read('vegetation-sets.v1.json')['sets']
def asset(p):
    raw=measured[p['id']]
    def curve(c,slope=False): return dict(min=max(0,c['minimum']) if slope else c['minimum'],optimum=c['optimum'],max=c['maximum'])
    return dict(id=p['id'],name=p['name'],prefabName=raw['prefabNameCandidate'] if 'prefabNameCandidate' in raw else p['id'].split(':')[1],placeholder=p.get('mappingStatus')=='unconfirmedMeshAnalogue',densityPercent=100,
      profile=dict(matureSpacingM=2*p['preferredSpacingRadiusM'],heightM=p['matureHeightM'],canopyRadiusM=p['crownRadiusM'],
        canopyOpacityPercent=p['crownOpacity']*100,shadeTolerancePercent=0,temperature=curve(p['temperature']),slope=curve(p['slope'],True),
        limits=dict(minElevationM=p['terrain']['minimumElevation'],maxElevationM=p['terrain']['maximumElevation'],maxSlopeDegrees=p['terrain']['maximumSlopeDegrees'])),
      ages=[dict(id=a['id'],name=a['id'].capitalize(),proportionPercent=a['weight']/sum(v['weight'] for v in p['ages'])*100) for a in p['ages']])
assets={p['id']:asset(p) for p in profiles}
plane='cs2:NA_LondonPlaneTree01'
def make_set(name,members,density=100):
    return dict(name=name,overallDensityPercent=density,clumpingPercent=0,climate={},limits=dict(minElevationM=-10000,maxElevationM=10000,maxSlopeDegrees=max(a['profile']['limits']['maxSlopeDegrees'] for a in members)),assets=members)
# Preserve the old single-asset library ID for existing workflows, using the new default profile.
sets=[dict(id='builtin:london-plane',builtIn=True,set=make_set('London Plane',[copy.deepcopy(assets[plane])],75))]
for preset in presets:
    members=[]
    for m in preset['members']:
        a=copy.deepcopy(assets[m['assetId']]); a['densityPercent']=m['weight']
        assert 0<a['densityPercent']<=100, 'UI percentage field currently supports preset weights up to 100'
        overrides=m.get('profileOverrides',{})
        if set(overrides) - {'temperature','slope','maximumSlopeDegrees'}:
            raise ValueError(f'Unsupported editor profile override for {m["assetId"]}')
        for key in ['temperature','slope']:
            if key in overrides:
                c=overrides[key]; a['profile'][key]=dict(min=max(0,c['minimum']) if key=='slope' else c['minimum'],optimum=c['optimum'],max=c['maximum'])
        if 'maximumSlopeDegrees' in overrides: a['profile']['limits']['maxSlopeDegrees']=overrides['maximumSlopeDegrees']
        members.append(a)
    sets.append(dict(id='builtin:'+preset['id'],builtIn=True,set=make_set(preset['name'],members,preset['overallDensityPercent'])))
catalog=dict(schemaVersion=2,disclaimer='Prototype vegetation profiles and visual proxy sets. Temperature is long-term annual mean; ecological curves are authored estimates. Windows validation of multi-asset prefab spawning remains outstanding.',
    cityDefaults=dict(source='manual',referenceTemperatureC=15,referenceElevationM=0,coolingCPerKm=6.5),
    assets=[assets[plane]]+[a for id,a in assets.items() if id!=plane],sets=sets)
text=json.dumps(catalog,indent=2,ensure_ascii=False)+'\n'
path=root/'data/editor-catalog.v2.json'
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');args=parser.parse_args()
if args.check:
    if not path.exists() or path.read_text()!=text: raise SystemExit('Editor catalogue is stale: run python3 scripts/build-editor-catalog.py')
else: path.write_text(text)
print(f'{len(catalog["assets"])} assets, {len(sets)} sets; catalogue '+('verified' if args.check else 'written'))
