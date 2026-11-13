import json,sys
m=json.load(open('tools/audio/build/measurements.json'))
f=sys.argv[1] if len(sys.argv)>1 else ''
print(f"{'file':44s} {'sec':>5} {'peak':>6} {'tp':>6} {'rms':>6} {'lufs':>6} {'cent':>6} {'atk':>6} {'aud':>5} sub low lm mid hm hi clip seam")
for k,v in m.items():
    if f not in k: continue
    print(f"{k[:44]:44s} {v['seconds']:5.2f} {v['peak_db']:6.1f} {v['true_peak_db']:6.1f} {v['rms_db']:6.1f} {str(v['lufs']):>6} {v['centroid_hz']:6.0f} {v['attack_ms']:6.1f} {v['audible_seconds']:5.2f} {v['sub_pct']:3.0f} {v['low_pct']:3.0f} {v['lowmid_pct']:2.0f} {v['mid_pct']:3.0f} {v['highmid_pct']:2.0f} {v['high_pct']:2.0f}  {v['clipped_samples']} {v.get('jump_ratio','')} {v.get('flux_ratio','')}")
