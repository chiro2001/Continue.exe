#!/usr/bin/env python3
"""Encode only the complete, verified V2 frames; never play the movie."""
from pathlib import Path

import encode_fullfilm_movie as encoder
from render_fullfilm_v2_audio import validate_reference

encoder.DEST = Path.home()/"tmp/vrc-old-web-map/fullfilm-v2/review"
encoder.BASE = r"C:\Users\Chiro\tmp\vrc-old-web-map\fullfilm-v2"
encoder.SCP_BASE = "C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v2"
encoder.FILENAME = "fullfilm-v2-review.mp4"
encoder.validate_audio = validate_reference

if __name__ == "__main__":
    raise SystemExit(encoder.main())
